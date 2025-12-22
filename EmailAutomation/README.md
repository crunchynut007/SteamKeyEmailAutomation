# Steam Key Email Automation

A dirty C# Console Application designed for game developers to batch-send unique Steam keys to influencers and press.

## Features
* **Safety First:** Pre-flight checks, data preview tables, and "Proceed/Abort" prompts.
* **Resume Capability:** Automatically filters CSV data to only send to rows marked `FALSE` in the "Sent" column.
* **Smart Parsing:** Handles complex CSV data (including commas inside quoted strings).
* **Rich HTML:** Supports beautiful HTML templates with CSS styling and automatic fallback to plain text.
* **Embedded Assets:** Supports inline logo attachments (no external image hosting required).
* **Subject Line Extraction:** Reads the email subject directly from the HTML `<title>` tag.

---

## Prerequisites

1.  **IDE:** [JetBrains Rider](https://www.jetbrains.com/rider/) (Recommended) or Visual Studio/Code.
2.  **.NET SDK:** .NET 6.0 or higher.
3.  **Gmail Account:**
    * You must enable **2-Factor Authentication**.
    * You must generate an **App Password** (Use this instead of your real password).
    * *Guide: [Sign in with App Passwords](https://support.google.com/accounts/answer/185833)*

---

## Installation

1.  **Clone the Repository**
    ```bash
    git clone [https://github.com/YourUsername/SteamKeyMailer.git](https://github.com/YourUsername/SteamKeyMailer.git)
    cd SteamKeyMailer
    ```

2.  **Restore Dependencies**
    The project uses `MailKit` and `MimeKit`. Restore them via your IDE or terminal:
    ```bash
    dotnet restore
    ```

---

## Configuration

**Note:** For security reasons, configuration files and user data are ignored by Git. You must create them manually.

### The Config File
Create a file named `config.json` in the project root (next to `Program.cs`).

```json
{
  "CsvDataFile": "contacts.csv",
  
  "InfluencerHtmlTemplate": "Templates/template_inf.html",
  "DeveloperHtmlTemplate": "Templates/template_dev.html",
  
  "InfluencerTextTemplate": "Templates/body_inf.txt",
  "DeveloperTextTemplate": "Templates/body_dev.txt",

  "SmtpServer": "smtp.gmail.com",
  "SmtpPort": 587,
  "SenderEmail": "your.email@gmail.com",
  "SenderName": "Your Studio Name",
  "AppPassword": "xxxx xxxx xxxx xxxx", 
  "DelayMs": 1500,
  "DefaultSubject": "Game Key for Review"
}
```
- AppPassword: The 16-character code from Google (spaces are allowed).
- DelayMs: Time to wait between emails to avoid spam filters (1500ms recommended).


### The Data File
Create a CSV file named `contacts.csv` in the project root. It should look like this:

```
Sent,Status,ChannelName,ContactEmail,SteamKeys
FALSE,Influencer,BestGamer,bg@example.com,AAAAA-BBBBB-CCCCC
FALSE,Developer,IndieDev,dev@example.com,11111-22222-33333
TRUE,Influencer,OldContact,old@example.com,ALREADY-SENT-CODE
```
- Sent: Must be FALSE to be processed. If TRUE, the row is skipped.
- Status: If set to Developer, the tool uses the Developer templates. Otherwise, it uses Influencer templates.

---

## Creating Templates

### HTML Templates
The tool automatically extracts the Email Subject from the `<title>` tag.


### Supported Placeholders:

- `{0}` : Insert Steam Key

- `{1}` : Insert Channel Name

- `{2}` : Insert Logo (src attribute) (Feature disabled - uncomment and add the logo image path to config)

#### Example (`template_inf.html`):
```html
<!DOCTYPE html>
<html>
<head>
    <title>Exclusive Key for {1}!</title>
</head>
<body style="background-color: #171a21; color: white;">
    <div style="text-align: center;">
        <img src="{2}" alt="Logo" width="200">
    </div>

    <h1>Hello {1}!</h1>
    <p>Here is your key:</p>
    <div style="background: black; padding: 10px; font-family: monospace;">
        {0}
    </div>
</body>
</html>
```

### Text Templates

Used as a fallback for strict spam filters or text-only clients.

#### Example (`body_inf.txt`):

```text
Hello {1}!

Here is your Steam key for review: {0}

Cheers,
Dev Team
```

---

## Execution

Build and run Project: 
- Ensure all your created files (config.json, contacts.csv, templates, and logo.png) are set to "Copy to Output Directory" in your IDE properties.
  - Rider: Right-click file → Properties → Copy to Output Directory → Copy if Newer.

### The Workflow:

1. The app loads the config and parses the CSV.
2. It filters out any rows where Sent is not FALSE.
3. It displays a Verification Table showing who will receive an email.
4. It asks for confirmation (Press Y to proceed).
5. It connects to SMTP and sends emails one by one with a delay.

---

## Troubleshooting

- Error: "Input string was not in a correct format"
  - Cause: You likely have CSS braces { } in your HTML template.
  - Fix: The code handles this safely using .Replace(), but ensure you aren't using other C# formatting methods.
- Error: CSV parsing is shifting columns
  - Cause: Your CSV data contains commas (e.g., "Action, RPG").
  - Fix: Ensure your CSV values are wrapped in quotes: "Action, RPG". The tool's built-in Regex parser handles this automatically.
- Error: SMTP Authentication Failed
  - Cause: You are using your real Google password.
  - Fix: You must use an App Password generated from the Google Security dashboard.
