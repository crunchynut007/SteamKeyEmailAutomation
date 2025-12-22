using System.Text.Json;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MimeKit;

namespace EmailAutomation
{
    // 1. Updated Config Structure
    public class AppConfig
    {
        // public string LogoFilePath { get; set; }
        public string CsvDataFile { get; set; }
        
        // HTML Templates
        public string InfluencerHtmlTemplate { get; set; }
        public string DeveloperHtmlTemplate { get; set; }
        
        // Plain Text Fallbacks
        public string InfluencerTextTemplate { get; set; }
        public string DeveloperTextTemplate { get; set; }

        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string SenderEmail { get; set; }
        public string SenderName { get; set; }
        public string AppPassword { get; set; }
        public int DelayMs { get; set; }
        public string DefaultSubject { get; set; } // (used only if HTML has no title)
    }

    // 2. Data Container for a single row
    public class ContactRow
    {
        public string Sent { get; set; }
        public string ChannelName { get; set; }
        public string Status { get; set; }
        public string Email { get; set; }
        public string SteamKey { get; set; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(SteamKey);
    }

    /// <summary>
    /// Instruction:
    /// 1. Update/Create config.json with your settings matching the AppConfig class.
    /// 2. Ensure the CSV file is formatted correctly
    /// 3. Ensure the HTML templates are formatted correctly.
    ///  - The title tag is used to extract the subject line.
    ///  - The {0} and {1} tags are used to replace the Steam Key and Channel Name.
    /// 4. The text templates are in case the html cannot be rendered.
    /// 5. Ensure the App Password is correct (search "App Passwords" in your Google Account -> Security)
    /// 6. Ensure all data files are in the same folder as the executable (in rider select them -> right click -> properties -> copy to output directory -> copy always)
    /// 7. Run the program
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // --- STEP 1: LOAD CONFIG ---
            string configPath = "config.json";
            if (!File.Exists(configPath))
            {
                Console.WriteLine("Error: config.json not found.");
                return;
            }
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath));
            if (config == null) return;
            
            // Logo File Check
            // if (!File.Exists(config.LogoFilePath))
            // {
            //     Console.WriteLine($"Error: Logo file '{config.LogoFilePath}' not found.");
            //     return;
            // }

            // --- STEP 2: LOAD TEMPLATES INTO MEMORY ---
            // We read these once so we don't hit the disk 500 times.
            Dictionary<string, string> templates = new Dictionary<string, string>();

            if (!TryLoadFile(config.InfluencerHtmlTemplate, out string influencerHtml)) return;
            if (!TryLoadFile(config.DeveloperHtmlTemplate, out string developerHtml)) return;
            if (!TryLoadFile(config.InfluencerTextTemplate, out string influencerText)) return;
            if (!TryLoadFile(config.DeveloperTextTemplate, out string developerText)) return;
            
            // Extract Subjects (New Logic)
            string influencerSubject = ExtractTitle(influencerHtml) ?? config.DefaultSubject;
            string developerSubject = ExtractTitle(developerHtml) ?? config.DefaultSubject;

            Console.WriteLine($"Influencer Subject: {influencerSubject}");
            Console.WriteLine($"Developer Subject: {developerSubject}");

            // --- STEP 3: PARSE CSV ---
            if (!File.Exists(config.CsvDataFile))
            {
                Console.WriteLine($"Error: CSV file '{config.CsvDataFile}' not found.");
                return;
            }
            
            // Get all contacts from CSV
            List<ContactRow> allContacts = ParseCsv(config.CsvDataFile);
            
            // Filter: Only keep rows where Sent is FALSE (or empty)
            // We use ToUpper() to make it case-insensitive (False, false, FALSE all work)
            List<ContactRow> contacts = allContacts
                .Where(c => c.Sent.ToUpper() == "FALSE") 
                .ToList();

            Console.WriteLine($"Parsed {allContacts.Count} rows. Found {contacts.Count} pending (FALSE).");
            
            
            // ---------------------------------------------------------
            // SAFETY CHECK / PROCEED PROMPT
            // ---------------------------------------------------------
            Console.WriteLine("\n========================================");
            Console.WriteLine($" PARSING COMPLETE");
            Console.WriteLine("========================================");
            Console.WriteLine($" Contacts Loaded:   {contacts.Count}");
            Console.WriteLine($" Skipped (Sent):    {allContacts.Count - contacts.Count}");
            Console.WriteLine($" Pending (FALSE):   {contacts.Count}"); // Only these will be shown/sent
            Console.WriteLine($" Delay per email:   {config.DelayMs}ms");
            Console.WriteLine("========================================");
            Console.WriteLine("");
            
            // ---------------------------------------------------------
            // DATA PREVIEW
            // ---------------------------------------------------------
            Console.WriteLine("====================================================================================================");
            Console.WriteLine($"{"SENT",-5} | {"STATUS",-12} | {"NAME",-25} | {"EMAIL",-30} | {"KEY PREVIEW",-20}");
            Console.WriteLine("====================================================================================================");

            int index = 1;
            foreach (var c in contacts)
            {
                // Truncate long strings for display cleanly
                string sent = c.Sent.Length > 5 ? c.Sent.Substring(0, 5) + ".." : c.Sent;
                string name = c.ChannelName.Length > 22 ? c.ChannelName.Substring(0, 22) + ".." : c.ChannelName;
                string email = c.Email.Length > 28 ? c.Email.Substring(0, 28) + ".." : c.Email;
                string keyPreview = c.SteamKey.Length > 20 ? c.SteamKey.Substring(0, 20) + "..." : c.SteamKey;

                Console.WriteLine($"{sent,-5} | {c.Status,-12} | {name,-25} | {email,-30} | {keyPreview,-20}");
                index++;
            }
            Console.WriteLine("====================================================================================================");
            
            if (contacts.Count == 0)
            {
                Console.WriteLine("Abort: No valid contacts found.");
                return;
            }

            Console.Write("\nReady to send? Press 'Y' to Proceed, or any other key to Abort: ");
            var key = Console.ReadKey();
            Console.WriteLine(); // New line for formatting

            if (key.Key != ConsoleKey.Y)
            {
                Console.WriteLine("Operation Aborted by user.");
                return;
            }
            
            // ---------------------------------------------------------

            // --- STEP 4: SENDING LOOP ---
            using (var client = new SmtpClient())
            {
                try
                {
                    Console.WriteLine("Connecting to SMTP...");
                    client.Connect(config.SmtpServer, config.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                    client.Authenticate(config.SenderEmail, config.AppPassword);
                    Console.WriteLine("Connected.");

                    int counter = 0;

                    foreach (var contact in contacts)
                    {
                        // Select Template based on Status
                        bool isDeveloper = contact.Status.Trim().Equals("Developer", StringComparison.OrdinalIgnoreCase);

                        string selectedHtml = isDeveloper ? developerHtml : influencerHtml;
                        string selectedText = isDeveloper ? developerText : influencerText;
                        string selectedSubject = isDeveloper ? developerSubject : influencerSubject;

                        var message = new MimeMessage();
                        message.From.Add(new MailboxAddress(config.SenderName, config.SenderEmail));
                        message.To.Add(new MailboxAddress(contact.ChannelName, contact.Email));
                        
                        // Apply the extracted subject
                        message.Subject = selectedSubject;

                        var builder = new BodyBuilder();
                        
                        // // 1. Attach the image as a "Linked Resource" (Inline)
                        // // This loads the file and creates a random Content-ID (e.g. "image1@xyz")
                        // var imageAttachment = builder.LinkedResources.Add(config.LogoFilePath);
                        //
                        // // 2. Construct the "src" attribute
                        // string imageCidSrc = $"cid:{imageAttachment.ContentId}";

                        // Perform Replacements
                        // Supported Tags: {0} = Steam Key, {1} = Channel Name
                        builder.HtmlBody = selectedHtml
                            .Replace("{0}", contact.SteamKey)
                            .Replace("{1}", contact.ChannelName);
                            // .Replace("{3}", imageCidSrc);

                        builder.TextBody = selectedText
                            .Replace("{0}", contact.SteamKey)
                            .Replace("{1}", contact.ChannelName);

                        message.Body = builder.ToMessageBody();

                        try
                        {
                            client.Send(message);
                            counter++;
                            Console.WriteLine($"[{counter}/{contacts.Count}] Sent ({contact.Status}) -> {contact.Email}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"FAILED -> {contact.Email}: {ex.Message}");
                        }

                        Thread.Sleep(config.DelayMs);
                    }

                    client.Disconnect(true);
                    Console.WriteLine("All Done.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CRITICAL SMTP ERROR: {ex.Message}");
                }
            }
        }

        // --- HELPER METHODS ---
        
        // Uses Regex to find <title>Anything Here</title>
        static string ExtractTitle(string htmlContent)
        {
            // Pattern: <title>, any characters (non-greedy), </title>
            // Options: IgnoreCase (handles <TITLE>)
            var match = Regex.Match(htmlContent, @"<title>\s*(.+?)\s*</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (match.Success)
            {
                return match.Groups[1].Value.Trim(); // Return the text inside the tags
            }
            return null; // Return null if tag is missing
        }

        // Simple helper to load file and log error if missing
        static bool TryLoadFile(string path, out string content)
        {
            content = "";
            if (!File.Exists(path))
            {
                Console.WriteLine($"Error: Template '{path}' missing.");
                return false;
            }
            content = File.ReadAllText(path);
            return true;
        }

        // Robust CSV Parser (No 3rd party packages)
        // Handles header mapping dynamically
        static List<ContactRow> ParseCsv(string filePath)
        {
            var results = new List<ContactRow>();
            var lines = File.ReadAllLines(filePath);

            if (lines.Length == 0) return results;

            // 1. Map Headers to Index
            var headerLine = lines[0];
            var headers = headerLine.Split(',');
            
            int idxSent = -1, idxName = -1, idxStatus = -1, idxEmail = -1, idxKey = -1;

            for (int i = 0; i < headers.Length; i++)
            {
                string h = headers[i].Trim().ToLower();
                if (h == "sent") idxSent = i;
                else if (h == "channelname") idxName = i;
                else if (h == "status") idxStatus = i;
                else if (h == "contactemail") idxEmail = i;
                else if (h == "steamkeys") idxKey = i;
            }

            // Check if required columns exist
            if (idxEmail == -1 || idxKey == -1)
            {
                Console.WriteLine("Error: CSV missing 'ContactEmail' or 'SteamKeys' header.");
                return results;
            }

            // 2. Parse Rows with Regex
            // This pattern finds commas that are NOT inside quotes
            var csvSplitter = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                // FIX: Use Regex split instead of string.Split
                var cols = csvSplitter.Split(line);

                if (cols.Length < headers.Length) continue;

                var row = new ContactRow
                {
                    // FIX: We must Trim('"') to remove the quotes around "Action, RPG"
                    Sent = (idxSent != -1 && idxSent < cols.Length) ? cols[idxSent].Trim().Trim('"') : "UNK",
                    ChannelName = (idxName != -1 && idxName < cols.Length) ? cols[idxName].Trim().Trim('"') : "Creator",
                    Status = (idxStatus != -1 && idxStatus < cols.Length) ? cols[idxStatus].Trim().Trim('"') : "Influencer",
                    Email = (idxEmail < cols.Length) ? cols[idxEmail].Trim().Trim('"') : "",
                    SteamKey = (idxKey < cols.Length) ? cols[idxKey].Trim().Trim('"') : ""
                };

                if (row.IsValid) results.Add(row);
            }

            return results;
        }
    }
}