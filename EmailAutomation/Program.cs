using System.Text.Json;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MimeKit;

namespace EmailAutomation
{
    // -------------------------------------------------------------------------
    // CONFIG
    // -------------------------------------------------------------------------
    public class AppConfig
    {
        public string CsvDataFile { get; set; }

        // Game info — drives all template tokens automatically
        public string SteamAppId                { get; set; }  // e.g. "4453320"
        public string GameTitle                 { get; set; }  // e.g. "Laballatory"
        public string GameReleaseDate           { get; set; }  // e.g. "Q3 2026"
        public string GameGenre                 { get; set; }  // e.g. "Incremental, Puzzle"
        public string GameShortDescription      { get; set; }  // one-liner shown in email body
        public string SteamGameImageUrl         { get; set; }  // header / capsule image src
        public string YoutubeGameplayTrailerUrl { get; set; }  // YouTube trailer link
        public string GoogleDrivePressKitUrl    { get; set; }  // Google Drive press kit folder

        // HTML Templates (one per email type)
        public string InfluencerHtmlTemplate  { get; set; }
        public string OutletHtmlTemplate   { get; set; }
        public string UnspecifiedHtmlTemplate { get; set; }

        // Plain-text fallbacks (one per email type)
        public string InfluencerTextTemplate  { get; set; }
        public string OutletTextTemplate   { get; set; }
        public string UnspecifiedTextTemplate { get; set; }

        // Steam Key partials — injected into {STEAM_KEY_SECTION} when HasKey == TRUE
        public string SteamKeyHtmlPartial { get; set; }  // e.g. "partial_steamkey.html"
        public string SteamKeyTextPartial { get; set; }  // e.g. "partial_steamkey.txt"

        public string SmtpServer   { get; set; }
        public int    SmtpPort     { get; set; }
        public string SenderEmail  { get; set; }
        public string SenderName   { get; set; }
        public string AppPassword  { get; set; }
        public int    DelayMs      { get; set; }
        public string DefaultSubject { get; set; }
    }

    // -------------------------------------------------------------------------
    // EMAIL TYPE ENUM
    // Maps from the CSV "Status" column value
    // -------------------------------------------------------------------------
    public enum EmailType { Influencer, Outlet, Unspecified }

    // -------------------------------------------------------------------------
    // CONTACT ROW
    // -------------------------------------------------------------------------
    public class ContactRow
    {
        public string    Sent        { get; set; }
        public string    ChannelName { get; set; }
        public EmailType Type        { get; set; }
        public string    Email       { get; set; }
        public bool      HasKey      { get; set; }  // from CSV "HasKey" column (TRUE/FALSE)
        public string    SteamKey    { get; set; }  // from CSV "SteamKey" column

        // A row is valid if it has an email
        public bool IsValid => !string.IsNullOrWhiteSpace(Email);

        // KeyReady: either key is not expected, or it is expected AND present
        public bool KeyReady => !HasKey || !string.IsNullOrWhiteSpace(SteamKey);
    }

    /// <summary>
    /// Unified email sender.
    ///
    /// EMAIL TYPE is determined per-row by the CSV "Status" column:
    ///   influencer  → Influencer templates
    ///   outlet      → Outlet templates
    ///   unspecified → Unspecified templates
    ///
    /// STEAM KEY SECTION is determined per-row by the CSV "ShouldSendKey" column:
    ///   TRUE  → builds the key section from partial files, injects at {STEAM_KEY_SECTION}
    ///   FALSE → {STEAM_KEY_SECTION} is replaced with an empty string
    ///
    /// ── TEMPLATE TOKENS ──────────────────────────────────────────────────────
    ///   {CHANNEL_NAME}          → Contact's channel / display name
    ///   {GAME_TITLE}            → config.GameTitle
    ///   {GAME_RELEASE_DATE}     → config.GameReleaseDate
    ///   {GAME_GENRE}            → config.GameGenre
    ///   {GAME_SHORT_DESCRIPTION}→ config.GameShortDescription
    ///   {GAME_IMAGE_URL}        → config.SteamGameImageUrl
    ///   {STORE_URL}             → https://store.steampowered.com/app/{config.SteamAppId}
    ///   {TRAILER_URL}           → config.YoutubeGameplayTrailerUrl
    ///   {PRESSKIT_URL}          → config.GoogleDrivePressKitUrl
    ///   {STEAM_KEY_SECTION}     → Injected partial (key box + redeem button) or empty string
    ///
    /// ── PARTIAL TOKENS (inside partial_steamkey.html / .txt only) ────────────
    ///   {STEAM_KEY}  → The raw key value
    ///   {STORE_URL}  → Same store URL (used for the redeem deep-link)
    ///
    /// ── SETUP ────────────────────────────────────────────────────────────────
    ///   1. Fill in config.json (see SAMPLE_config.json).
    ///   2. Ensure CSV columns: Sent, ShouldSendKey, SteamKeys, ChannelName, Status, ContactEmail
    ///   3. Templates use the tokens above — the <title> tag sets the subject line.
    ///   4. Set all data files to "Copy to Output Directory" in Rider.
    ///   5. Run.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            // ── LOAD CONFIG ───────────────────────────────────────────────────
            const string configPath = "config.json";
            if (!File.Exists(configPath))
            {
                Console.WriteLine("Error: config.json not found.");
                return;
            }

            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(configPath));
            if (config == null) return;

            if (string.IsNullOrWhiteSpace(config.SteamAppId))
            {
                Console.WriteLine("Error: 'SteamAppId' is missing from config.json.");
                return;
            }

            // Build the canonical store URL once
            string storeUrl = $"https://store.steampowered.com/app/{config.SteamAppId}";

            Console.WriteLine($"Game        : {config.GameTitle}");
            Console.WriteLine($"Store URL   : {storeUrl}");
            Console.WriteLine($"Trailer URL : {config.YoutubeGameplayTrailerUrl}");

            // ── LOAD TEMPLATES ────────────────────────────────────────────────
            if (!TryLoadFile(config.InfluencerHtmlTemplate,  out string influencerHtml))  { Console.WriteLine("Missing influencerHtml"); return;}
            if (!TryLoadFile(config.OutletHtmlTemplate,   out string outletHtml))         { Console.WriteLine("Missing outletHtml"); return;}
            if (!TryLoadFile(config.UnspecifiedHtmlTemplate, out string unspecifiedHtml)) { Console.WriteLine("Missing unspecifiedHtml"); return;}
            if (!TryLoadFile(config.InfluencerTextTemplate,  out string influencerText))  { Console.WriteLine("Missing influencerText"); return;}
            if (!TryLoadFile(config.OutletTextTemplate,   out string outletText))         { Console.WriteLine("Missing outletText"); return;}
            if (!TryLoadFile(config.UnspecifiedTextTemplate, out string unspecifiedText)) { Console.WriteLine("Missing unspecifiedText"); return;}

            // ── LOAD STEAM KEY PARTIALS ───────────────────────────────────────
            if (!TryLoadFile(config.SteamKeyHtmlPartial, out string keyHtmlPartial)) { Console.WriteLine("Missing keyHtmlPartial"); return;}
            if (!TryLoadFile(config.SteamKeyTextPartial, out string keyTextPartial)) { Console.WriteLine("Missing keyTextPartial"); return;}

            // ── EXTRACT SUBJECTS FROM <title> TAGS ───────────────────────────
            string influencerSubject  = ExtractTitle(influencerHtml)  ?? config.DefaultSubject;
            string outletSubject      = ExtractTitle(outletHtml)      ?? config.DefaultSubject;
            string unspecifiedSubject = ExtractTitle(unspecifiedHtml) ?? config.DefaultSubject;

            Console.WriteLine($"\nInfluencer Subject  : {influencerSubject}");
            Console.WriteLine($"Outlet Subject      : {outletSubject}");
            Console.WriteLine($"Unspecified Subject : {unspecifiedSubject}");

            // ── PARSE CSV ─────────────────────────────────────────────────────
            if (!File.Exists(config.CsvDataFile))
            {
                Console.WriteLine($"Error: CSV file '{config.CsvDataFile}' not found.");
                return;
            }

            List<ContactRow> allContacts = ParseCsv(config.CsvDataFile);
            List<ContactRow> pending     = allContacts.Where(c => c.Sent.ToUpper() == "FALSE").ToList();

            // Warn about HasKey=TRUE rows that are missing the actual key value
            List<ContactRow> missingKeys = pending.Where(c => !c.KeyReady).ToList();
            if (missingKeys.Count > 0)
            {
                Console.WriteLine($"\n[WARNING] {missingKeys.Count} row(s) have ShouldSendKey=TRUE but an empty SteamKeys — they will be SKIPPED:");
                foreach (var m in missingKeys)
                    Console.WriteLine($"  !! {Trunc(m.ChannelName, 25)} <{m.Email}>");
            }

            // Final send list: pending rows that are key-ready
            List<ContactRow> contacts = pending.Where(c => c.KeyReady).ToList();

            int alreadySent = allContacts.Count - pending.Count;

            // ── SAFETY CHECK / PROCEED PROMPT ─────────────────────────────────
            Console.WriteLine("\n==========================================");
            Console.WriteLine(" PARSING COMPLETE");
            Console.WriteLine("==========================================");
            Console.WriteLine($" Pending (FALSE)  : {contacts.Count}");
            Console.WriteLine($" Skipped (Sent)   : {alreadySent}");
            Console.WriteLine($" Skipped (No Key) : {missingKeys.Count}");
            Console.WriteLine($" Delay per email  : {config.DelayMs}ms");
            Console.WriteLine("==========================================\n");

            // Data preview table
            string header = $"{"SENT",-5} | {"TYPE",-12} | {"HAS KEY",-7} | {"NAME",-22} | {"EMAIL",-30} | {"KEY PREVIEW",-18}";
            string divider = new string('=', header.Length);

            Console.WriteLine(divider);
            Console.WriteLine(header);
            Console.WriteLine(divider);

            foreach (var c in contacts)
            {
                string keyPreview = c.HasKey ? Trunc(c.SteamKey, 18) : "-";
                Console.WriteLine(
                    $"{Trunc(c.Sent, 5),-5} | " +
                    $"{c.Type,-12} | " +
                    $"{(c.HasKey ? "YES" : "NO"),-7} | " +
                    $"{Trunc(c.ChannelName, 22),-22} | " +
                    $"{Trunc(c.Email, 30),-30} | " +
                    $"{keyPreview,-18}");
            }
            Console.WriteLine(divider);

            if (contacts.Count == 0)
            {
                Console.WriteLine("Abort: No valid contacts found.");
                return;
            }

            Console.Write("\nReady to send? Press 'Y' to Proceed, or any other key to Abort: ");
            var confirmKey = Console.ReadKey();
            Console.WriteLine();

            if (confirmKey.Key != ConsoleKey.Y)
            {
                Console.WriteLine("Operation Aborted by user.");
                return;
            }

            // ── SENDING LOOP ──────────────────────────────────────────────────
            using var client = new SmtpClient();
            try
            {
                Console.WriteLine("Connecting to SMTP...");
                client.Connect(config.SmtpServer, config.SmtpPort, MailKit.Security.SecureSocketOptions.StartTls);
                client.Authenticate(config.SenderEmail, config.AppPassword);
                Console.WriteLine("Connected.\n");

                int counter = 0;

                foreach (var contact in contacts)
                {
                    // Select template set by email type
                    var (baseHtml, baseText, subject) = contact.Type switch
                    {
                        EmailType.Outlet      => (outletHtml,      outletText,      outletSubject),
                        EmailType.Unspecified => (unspecifiedHtml, unspecifiedText, unspecifiedSubject),
                        _                     => (influencerHtml,  influencerText,  influencerSubject)
                    };

                    // Build the steam key section (or leave empty if no key)
                    string keyHtmlSection = contact.HasKey
                        ? keyHtmlPartial
                            .Replace("{STEAM_KEY}", contact.SteamKey)
                        : string.Empty;

                    string keyTextSection = contact.HasKey
                        ? keyTextPartial
                            .Replace("{STEAM_KEY}", contact.SteamKey)
                        : string.Empty;

                    // Apply all tokens to the full template
                    string finalHtml = ApplyTokens(baseHtml, contact, config, storeUrl, keyHtmlSection);
                    string finalText = ApplyTokens(baseText, contact, config, storeUrl, keyTextSection);

                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress(config.SenderName, config.SenderEmail));
                    message.To.Add(new MailboxAddress(contact.ChannelName, contact.Email));
                    message.Subject = subject;
                    message.Body = new BodyBuilder { HtmlBody = finalHtml, TextBody = finalText }.ToMessageBody();

                    try
                    {
                        client.Send(message);
                        counter++;
                        string keyLabel = contact.HasKey ? "w/ key" : "no key";
                        Console.WriteLine($"[{counter}/{contacts.Count}] Sent ({contact.Type}, {keyLabel}) -> {contact.Email}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"FAILED -> {contact.Email}: {ex.Message}");
                    }

                    Thread.Sleep(config.DelayMs);
                }

                client.Disconnect(true);
                Console.WriteLine("\nAll Done.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRITICAL SMTP ERROR: {ex.Message}");
            }
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        /// <summary>Replaces all shared template tokens in one pass.</summary>
        static string ApplyTokens(string template, ContactRow contact, AppConfig config,
                                   string storeUrl, string keySection)
        {
            return template
                .Replace("{CHANNEL_NAME}",           contact.ChannelName)
                .Replace("{GAME_TITLE}",              config.GameTitle                 ?? string.Empty)
                .Replace("{GAME_RELEASE_DATE}",       config.GameReleaseDate           ?? string.Empty)
                .Replace("{GAME_GENRE}",              config.GameGenre                 ?? string.Empty)
                .Replace("{GAME_SHORT_DESCRIPTION}",  config.GameShortDescription      ?? string.Empty)
                .Replace("{GAME_IMAGE_URL}",          config.SteamGameImageUrl         ?? string.Empty)
                .Replace("{STORE_URL}",               storeUrl)
                .Replace("{TRAILER_URL}",             config.YoutubeGameplayTrailerUrl ?? string.Empty)
                .Replace("{PRESSKIT_URL}",            config.GoogleDrivePressKitUrl    ?? string.Empty)
                .Replace("{STEAM_KEY_SECTION}",       keySection);
        }

        /// <summary>Truncates a string for fixed-width console display.</summary>
        static string Trunc(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length > max ? s.Substring(0, max - 2) + ".." : s;
        }

        /// <summary>Extracts the content of the HTML &lt;title&gt; tag.</summary>
        static string ExtractTitle(string html)
        {
            var match = Regex.Match(html, @"<title>\s*(.+?)\s*</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        /// <summary>Loads a file's text content, printing an error if missing.</summary>
        static bool TryLoadFile(string path, out string content)
        {
            content = string.Empty;
            if (!File.Exists(path))
            {
                Console.WriteLine($"Error: File '{path}' not found.");
                return false;
            }
            content = File.ReadAllText(path);
            return true;
        }

        /// <summary>
        /// Parses the CSV into ContactRow objects.
        /// Required columns : Sent, ShouldSendKey, ContactEmail
        /// Optional columns : Status, ChannelName, SteamKeys
        /// Ignored columns  : ChannelFocus, Notes (and any other unrecognised header)
        /// </summary>
        static List<ContactRow> ParseCsv(string filePath)
        {
            var results = new List<ContactRow>();
            var lines   = File.ReadAllLines(filePath);
            if (lines.Length == 0) return results;

            // Map header names to column indices
            string[] headers = lines[0].Split(',');
            int idxSent = -1, idxName = -1, idxStatus = -1, idxEmail = -1,
                idxHasKey = -1, idxKey = -1;

            for (int i = 0; i < headers.Length; i++)
            {
                switch (headers[i].Trim().ToLower())
                {
                    case "sent":          idxSent   = i; break;
                    case "channelname":   idxName   = i; break;
                    case "status":        idxStatus = i; break;
                    case "contactemail":  idxEmail  = i; break;
                    case "shouldsendkey": idxHasKey = i; break;  // renamed from HasKey
                    case "steamkeys":     idxKey    = i; break;  // renamed from SteamKey
                    // ChannelFocus and Notes are intentionally ignored
                }
            }

            // Validate required headers
            if (idxEmail == -1)
            {
                Console.WriteLine("Error: CSV missing required 'ContactEmail' column.");
                return results;
            }
            if (idxHasKey == -1)
            {
                Console.WriteLine("Error: CSV missing required 'ShouldSendKey' column.");
                return results;
            }

            // Regex splits on commas that are NOT inside double-quotes
            var csvSplitter = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                string[] cols = csvSplitter.Split(lines[i]);
                if (cols.Length < headers.Length) continue;

                string Col(int idx) => (idx != -1 && idx < cols.Length)
                    ? cols[idx].Trim().Trim('"')
                    : string.Empty;

                // Parse Status → EmailType
                EmailType type = Col(idxStatus).ToLower() switch
                {
                    "outlet"      => EmailType.Outlet,
                    "unspecified" => EmailType.Unspecified,
                    _             => EmailType.Influencer   // default
                };

                var row = new ContactRow
                {
                    Sent        = Col(idxSent) is { Length: > 0 } s ? s : "UNK",
                    ChannelName = Col(idxName) is { Length: > 0 } n ? n : "Creator",
                    Type        = type,
                    Email       = Col(idxEmail),
                    HasKey      = Col(idxHasKey).ToUpper() == "TRUE",
                    SteamKey    = Col(idxKey)
                };

                if (row.IsValid) results.Add(row);
            }

            return results;
        }
    }
}