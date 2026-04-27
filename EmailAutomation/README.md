# Game Press Email Automation

A filthy C# Console Application designed for indie game developers to batch-send press and influencer outreach emails — with or without Steam keys — to a recipient list sourced from a CSV file.

## Features

- **Three Email Types:** Tailored templates for `influencer`, `outlet` (gaming press), and `unspecified` contacts, selected per-row from the CSV.
- **Conditional Steam Key Injection:** A `ShouldSendKey` column in the CSV controls whether the key box and redeem button are injected into the email — no separate send runs needed.
- **Named Token System:** Templates use readable named tokens like `{GAME_TITLE}` and `{PRESSKIT_URL}` rather than positional placeholders.
- **Game Info in Config:** All game details (title, genre, description, store URL, trailer, press kit) live in `config.json` — update once, reflected everywhere.
- **Safety First:** Pre-flight checks, a data preview table, and a Proceed/Abort confirmation prompt before any email is sent.
- **Resume Capability:** Rows marked `TRUE` in the `Sent` column are automatically skipped, so interrupted runs can be safely re-run.
- **Smart CSV Parsing:** Handles commas inside quoted fields without shifting columns.
- **Subject Line Extraction:** Reads the email subject directly from the HTML `<title>` tag.
- **Plain Text Fallback:** Every HTML template has a matching `.txt` file for clients that can't render HTML.

---

## Prerequisites

1. **IDE:** [JetBrains Rider](https://www.jetbrains.com/rider/) (Recommended) or Visual Studio / VS Code.
2. **.NET SDK:** .NET 6.0 or higher.
3. **Gmail Account:**
    - Enable **2-Factor Authentication**.
    - Generate an **App Password** — use this instead of your real password.
    - Guide: [Sign in with App Passwords](https://support.google.com/accounts/answer/185833)

---

## Installation

1. **Clone the Repository**
   ```bash
   git clone https://github.com/YourUsername/GamePressMailer.git
   cd GamePressMailer
   ```

2. **Restore Dependencies**
   The project uses `MailKit` and `MimeKit`. Restore via your IDE or terminal:
   ```bash
   dotnet restore
   ```

---

## Configuration

> **Note:** `config.json` and `contacts.csv` are excluded from Git for security. Create them manually using the samples below as a guide.

### `config.json`

Create `config.json` in the project root (next to `Program.cs`):

```json
{
  "CsvDataFile": "contacts.csv",

  "SteamAppId":                "4453320",
  "GameTitle":                 "AwesomeGame",
  "GameReleaseDate":           "Q3 2056",
  "GameGenre":                 "Action, Adventure, Platformer",
  "GameShortDescription":      "A casual sci-fi sandbox...",
  "SteamGameImageUrl":         "https://shared.fastly.steamstatic.com/...",
  "YoutubeGameplayTrailerUrl": "https://youtu.be/yourTrailerId",
  "GoogleDrivePressKitUrl":    "https://drive.google.com/drive/folders/yourFolderId",

  "DefaultSubject": "Check out our latest game!",

  "InfluencerHtmlTemplate":  "template_influencer.html",
  "DeveloperHtmlTemplate":   "template_outlet.html",
  "UnspecifiedHtmlTemplate": "template_unspecified.html",

  "InfluencerTextTemplate":  "body_influencer.txt",
  "DeveloperTextTemplate":   "body_outlet.txt",
  "UnspecifiedTextTemplate": "body_unspecified.txt",

  "SteamKeyHtmlPartial": "partial_steamkey.html",
  "SteamKeyTextPartial": "partial_steamkey.txt",

  "SmtpServer":  "smtp.gmail.com",
  "SmtpPort":    587,
  "SenderEmail": "your.email@gmail.com",
  "SenderName":  "Your Studio Name",
  "AppPassword": "xxxx xxxx xxxx xxxx",
  "DelayMs":     1500
}
```

#### Field Reference

| Field | Description |
|---|---|
| `CsvDataFile` | Path to your contacts CSV file |
| `SteamAppId` | Your Steam App ID — used to build the store URL automatically |
| `GameTitle` | Game name — injected via `{GAME_TITLE}` |
| `GameReleaseDate` | Release window — injected via `{GAME_RELEASE_DATE}` |
| `GameGenre` | Genre string — injected via `{GAME_GENRE}` |
| `GameShortDescription` | One-liner description — injected via `{GAME_SHORT_DESCRIPTION}` |
| `SteamGameImageUrl` | Header/capsule image URL — injected via `{GAME_IMAGE_URL}` |
| `YoutubeGameplayTrailerUrl` | Trailer link — injected via `{TRAILER_URL}` |
| `GoogleDrivePressKitUrl` | Press kit folder link — injected via `{PRESSKIT_URL}` |
| `DefaultSubject` | Fallback subject if the HTML `<title>` tag is missing |
| `InfluencerHtmlTemplate` | HTML template path for `influencer` contacts |
| `DeveloperHtmlTemplate` | HTML template path for `outlet` contacts |
| `UnspecifiedHtmlTemplate` | HTML template path for `unspecified` contacts |
| `InfluencerTextTemplate` | Plain-text fallback for `influencer` contacts |
| `DeveloperTextTemplate` | Plain-text fallback for `outlet` contacts |
| `UnspecifiedTextTemplate` | Plain-text fallback for `unspecified` contacts |
| `SteamKeyHtmlPartial` | HTML snippet injected when `ShouldSendKey` is `TRUE` |
| `SteamKeyTextPartial` | Plain-text snippet injected when `ShouldSendKey` is `TRUE` |
| `AppPassword` | 16-character Google App Password (spaces are fine) |
| `DelayMs` | Milliseconds between emails — `1500` recommended to avoid spam filters |

---

### `contacts.csv`

Create `contacts.csv` in the project root. The column order doesn't matter — headers are mapped by name.

```
Sent,ShouldSendKey,SteamKeys,ChannelName,Status,ChannelFocus,ContactEmail,Notes
FALSE,TRUE,AAAAA-BBBBB-CCCCC,BestGamer,influencer,Gaming,bg@example.com,Loves platformers
FALSE,FALSE,,AnotherCreator,influencer,Variety,creator@example.com,
FALSE,TRUE,11111-22222-33333,IndieDev Monthly,outlet,Press,press@indiedev.com,
FALSE,FALSE,,GameReviewSite,outlet,Press,reviews@site.com,
FALSE,TRUE,DDDDD-EEEEE-FFFFF,MysteryContact,unspecified,,mystery@example.com,
TRUE,TRUE,ALREADY-SENT,OldContact,influencer,Gaming,old@example.com,Already handled
```

#### Column Reference

| Column | Required | Description |
|---|---|---|
| `Sent` | Yes | `FALSE` = will be sent. `TRUE` = skipped. |
| `ShouldSendKey` | Yes | `TRUE` = inject the Steam key section. `FALSE` = send without it. |
| `SteamKeys` | Conditional | Required when `ShouldSendKey` is `TRUE`. Rows with `ShouldSendKey=TRUE` but an empty `SteamKeys` are warned about and skipped. |
| `ChannelName` | No | Display name used in the email greeting. Defaults to `"Creator"` if missing. |
| `Status` | No | `influencer`, `outlet`, or `unspecified`. Determines which template set is used. Defaults to `influencer`. |
| `ChannelFocus` | — | Ignored by the tool. For your own reference. |
| `ContactEmail` | Yes | The recipient's email address. |
| `Notes` | — | Ignored by the tool. For your own reference. |

---

## Templates

All templates live alongside `Program.cs` (or wherever you point the config). Every file must be set to **Copy to Output Directory** in Rider (`Right-click → Properties → Copy to Output Directory → Copy if Newer`).

### File Overview

| File | Purpose |
|---|---|
| `template_influencer.html` | HTML email for influencer contacts |
| `template_outlet.html` | HTML email for gaming press / outlet contacts |
| `template_unspecified.html` | HTML email for contacts with no specific type |
| `body_influencer.txt` | Plain-text fallback for influencer contacts |
| `body_outlet.txt` | Plain-text fallback for outlet contacts |
| `body_unspecified.txt` | Plain-text fallback for unspecified contacts |
| `partial_steamkey.html` | HTML key box + redeem button — injected when `ShouldSendKey=TRUE` |
| `partial_steamkey.txt` | Plain-text key block — injected when `ShouldSendKey=TRUE` |

### Token Reference

Use these named tokens anywhere in your templates or partial files:

| Token | Value |
|---|---|
| `{CHANNEL_NAME}` | Contact's channel / display name |
| `{GAME_TITLE}` | `config.GameTitle` |
| `{GAME_RELEASE_DATE}` | `config.GameReleaseDate` |
| `{GAME_GENRE}` | `config.GameGenre` |
| `{GAME_SHORT_DESCRIPTION}` | `config.GameShortDescription` |
| `{GAME_IMAGE_URL}` | `config.SteamGameImageUrl` |
| `{STORE_URL}` | Built from `config.SteamAppId` → `https://store.steampowered.com/app/{SteamAppId}` |
| `{TRAILER_URL}` | `config.YoutubeGameplayTrailerUrl` |
| `{PRESSKIT_URL}` | `config.GoogleDrivePressKitUrl` |
| `{STEAM_KEY_SECTION}` | Replaced with the compiled partial, or an empty string if `ShouldSendKey=FALSE` |

The following tokens are only available **inside the partial files** (`partial_steamkey.html` / `.txt`):

| Token | Value |
|---|---|
| `{STEAM_KEY}` | The raw Steam key value from the CSV |
| `{STORE_URL}` | Same store URL — used to build the deep-link redeem button |

### Subject Line

The email subject is extracted automatically from the HTML `<title>` tag of each template:

```html
<title>[{GAME_TITLE}] Check out our new upcoming game!</title>
```

If the `<title>` tag is missing, `config.DefaultSubject` is used as a fallback.

---

## Execution

### The Workflow

1. The app loads and validates `config.json`.
2. All six templates and both partial files are loaded.
3. The CSV is parsed and filtered — only rows with `Sent=FALSE` are considered.
4. Rows with `ShouldSendKey=TRUE` but an empty `SteamKeys` value are flagged as warnings and excluded.
5. A preview table is displayed showing every contact that will receive an email, their type, and whether a key is included.
6. You are prompted to press `Y` to proceed or any other key to abort.
7. The app connects to SMTP and sends emails one by one, with a configurable delay between each.

### Preview Table

Before sending, the app prints a table like this:

```
==========================================================================
SENT  | TYPE         | HAS KEY | NAME                   | EMAIL                          | KEY PREVIEW
==========================================================================
FALSE | Influencer   | YES     | BestGamer              | bg@example.com                 | AAAAA-BBBBB-CCCCC
FALSE | Influencer   | NO      | AnotherCreator         | creator@example.com            | -
FALSE | Outlet       | YES     | IndieDev Monthly       | press@indiedev.com             | 11111-22222-333..
==========================================================================
```

---

## Troubleshooting

**Error: CSV parsing is shifting columns**
- Cause: A CSV field contains a comma (e.g., a genre like `Incremental, Puzzle`).
- Fix: Wrap the value in double quotes in the CSV: `"Incremental, Puzzle"`. The built-in Regex parser handles this automatically.

**Warning: Row skipped due to missing Steam key**
- Cause: A row has `ShouldSendKey=TRUE` but the `SteamKeys` column is empty.
- Fix: Add the Steam key to that row in your CSV, or set `ShouldSendKey=FALSE` if you don't intend to send one.

**Error: SMTP Authentication Failed**
- Cause: You are using your real Google account password.
- Fix: Generate an **App Password** from Google Account → Security → App Passwords, and use that 16-character code in `config.AppPassword`.

**Error: File '...' not found**
- Cause: A template or partial file path in `config.json` is wrong, or the file isn't set to copy to the output directory.
- Fix: Check that all file paths in `config.json` are correct and that each file is set to *Copy to Output Directory* in Rider.

**Emails land in spam**
- Increase `DelayMs` (try `2000` or higher).
- Ensure your Gmail account is warmed up and not sending from a brand-new address.
- Avoid spam trigger words in your subject line.