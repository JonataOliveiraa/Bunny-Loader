# Privacy Policy for Bunny Loader

**Last updated: October 25, 2026**

> **Developer verification required before final publication.** This draft is
> based on a review of the available application source. The items in
> [Developer verification before publication](#developer-verification-before-publication)
> remain to be confirmed for the distributed application. This document does
> not certify legal compliance or Google Play approval.

Bunny Loader is a free mod loader for Terraria Mobile. This policy explains
how the Android application, identified by the package name
`com.bunnyloader`, handles information when you use its launcher, install mods,
and play the game. The project is maintained through the
[Bunny Loader GitHub repository](https://github.com/JonataOliveiraa/Bunny-Loader).

We want you to understand what stays on your device, what is involved in an
online connection, and the choices available to you. A local profile name or
photo, a game save, or an IP address can contain or constitute personal
information. Some information is processed locally, while online features,
backups, and services you choose can involve transfers outside your device.

## 1. Information used on your device

Bunny Loader accesses and processes the following information to provide its
features:

- **Game saves and related mod data.** The application reads and writes
  character and world files, including names and information needed to list
  saves, start a game, and preserve modded content. When you import a save,
  it copies the files you select into its own save folders.
- **Installed mods and resource packs.** The application stores and reads
  imported or downloaded packages, scripts, textures, fonts, package
  descriptions, and mod configuration or save data. It also remembers which
  packages are enabled and their loading order.
- **Preferences and optional profile details.** Settings include language,
  appearance, diagnostic options, and quick-start character or world choices.
  You may also enter a local display name and select a profile image. The
  selected image is resized and saved in the application's internal storage.
  This local profile does not create a Bunny Loader online account.
- **Catalog cache.** Catalog listings, mod versions, descriptions, images,
  download totals, and cache-validation information are stored locally to
  display the catalog and reduce repeated downloads.
- **Diagnostics.** The application creates local startup, session, and error
  logs. A diagnostic report can include device manufacturer and model,
  Android version, processor architecture, app and game versions, timestamps,
  enabled mod names and versions, errors, and stack traces. Logs can also
  contain file paths, server addresses, game information, and text written by
  mods.
- **Game installation information.** The launcher checks whether the Terraria
  Android package is installed. Supporting runtime code can read its package
  version, installation paths, and supported architecture to locate and
  validate the game environment.

Mods, saves, and session logs normally use folders under
`Android/data/com.bunnyloader/`, including `bunny_packs/`, `Players/`,
`Worlds/`, and `logs/`. Preferences, the profile image, startup diagnostics,
and cached content use application storage managed by Android. Storage
locations can vary with the device and storage availability.

File and image import features use Android's system picker to read the items
you choose. If you select a cloud-backed document provider or export a file
to one, that provider's own data handling also applies.

## 2. Internet access, downloads, and updates

The launcher can refresh the online catalog when it opens or returns to the
foreground, as well as when you request a refresh. It uses network connections
to retrieve catalog listings, mod descriptions and images, and aggregate
download totals. Installing or updating a catalog mod downloads its package.
Optional multiplayer mod synchronization may also download required packages.

The configured catalog is the public
[Bunny Loader Mods repository](https://github.com/JonataOliveiraa/Bunny-Loader-Mods).
Catalog content is served through GitHub, including
`raw.githubusercontent.com`; package downloads use GitHub Releases and may
redirect to GitHub's download infrastructure. Some older packages may be
downloaded from Releases in the Bunny Loader application repository.

Catalog requests do not require a Bunny Loader account or a GitHub sign-in.
The downloader sends a `BunnyLoader` user-agent header and may send a cached
`ETag` value to check whether content has changed. It does not attach your
local profile, saves, preferences, or diagnostic reports to these requests.
Mod-update availability is determined by comparing catalog versions with
versions stored on your device.

The reviewed network implementation checks for **mod updates**. Any separate
mechanism for checking or installing updates to the Bunny Loader application
itself must be verified for the distributed build, as listed below.

## 3. Information received by hosting services and external sites

Making an internet request exposes connection information to the receiving
service. GitHub and the infrastructure serving a request receive your network
IP address, the requested resource, and request headers. Request times and
other technical information may also be logged. A resource path may indicate
which mod page or package was requested; using a public catalog without
signing in does not make a connection untraceable.

GitHub describes its processing, retention, and privacy controls in the
[GitHub General Privacy Statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement).
Its logging practices and retention periods should not be assumed to match
those of Bunny Loader.

Links to the project, community pages, a mod author's website, or the Play
Store may open an external application or browser. Those services receive
information associated with your visit and handle it under their own policies.

No advertising SDK, behavioral analytics integration, or automatic remote
crash-report uploader was identified in the reviewed loader source and
declared app dependencies. This is a limited source-review finding: the
bundled game runtime, native libraries, and bundled mods still require the
release-specific verification described below.

## 4. Multiplayer and third-party mods

When you join or host a multiplayer game, the game exchanges connection and
gameplay information with the server or other participants. The receiving
host can receive your connection IP address and the information exchanged
during that session. Bunny Loader's mod synchronization sends active mod
identifiers, names, and versions, together with a texture-pack count, to check
compatibility with the host. Mods can exchange additional gameplay or
mod-specific messages.

If you accept server mod synchronization, the application temporarily writes
a reconnection file in its internal storage. This file includes the server
address and port, server password if one was entered, selected character
filename, and the mod list needed for the restart. It is deleted when the
next game startup reads it. If that startup does not occur, the file can remain
until it is consumed or the application's storage is cleared. The current
implementation writes this file as JSON without additional application-level
encryption.

Mods contain code written by their authors and run inside the game process.
They can read or change game and mod data and may use available game APIs for
file access or network communication, subject to Android's restrictions.
Installing a mod does not give it a separate Android privacy boundary from
the game process. Its behavior may therefore differ from the launcher's own
catalog and diagnostic features.

Please install mods from authors you trust, review any privacy information
they provide, and disable or remove a mod if its behavior concerns you. This
policy describes the loader's known behavior; it cannot establish the data
practices of every independently authored mod. Bundled third-party code must
also be reviewed by the developer before the application is distributed.

## 5. Diagnostics and information you choose to share

The launcher provides a diagnostic report that you can view and copy to the
Android clipboard. The reviewed diagnostic feature does not automatically
send that report to the developer. If you paste it into an issue, chat, or
another application, that destination receives what you share.

Please review reports, screenshots, saves, and mod packages before sharing
them. Remove information you do not want others to see, including passwords,
personal names, and private server details. Public GitHub issues and their
attachments can be viewed by other people.

If you contact the project through GitHub, the maintainer receives the
information you submit, such as your GitHub username, message, and attachments,
to handle your inquiry. GitHub also processes that interaction under its own
privacy policy. Retention of any separate developer-held support records
requires confirmation before this draft is finalized.

## 6. Storage protection and Android backups

The loader uses Android application storage and system file-selection
features. Android controls access to these locations according to the device's
version, permissions, and security settings. Configured GitHub catalog and
package URLs use HTTPS. Downloaded catalog packages are checked against the
catalog's SHA-256 value, and archive import checks prevent entries from
writing outside the intended extraction folder. These checks help protect
downloads and imports; they do not establish that a mod's code is safe.

The application does not add its own encryption layer to its general local
files. Protection also depends on your device, operating system, installed
mods, and any apps or services to which you grant access. No storage or
transmission method can be guaranteed completely secure.

**Android backup is enabled in the current application manifest.** Depending
on your Android version, device settings, and backup or transfer provider,
eligible application data may be copied to a cloud backup service or another
device. For example, Android's backup rules can include shared preferences
and internal application files. These transfers are controlled by the Android
backup system, rather than by the catalog downloader. Review your device's
backup settings and the provider's privacy information. The exact backup
scope, including the temporary reconnection file, needs developer verification.

## 7. Retention, control, and deletion

Local saves, installed packages, preferences, and profile details generally
remain until you change or delete them, clear application storage, or remove
the application. Catalog and download caches may be replaced or cleared as
content changes or Android reclaims storage. Loader-managed session logs
rotate to keep up to three recent sessions; startup and captured process logs
are reset when a new game session starts. Mods may create additional files
with their own retention behavior.

You can manage local information in the following ways:

- **Manage mods in the launcher.** Disable packages to stop loading them, or
  uninstall them to remove their installed package folder. Disabling is not
  deletion. Uninstalling a package does not remove data that it has written
  elsewhere, such as information in a world or character save.
- **Change preferences and profile details.** Update your settings, edit the
  local display name, or replace the profile image in the launcher. Clearing
  application storage removes the internally stored profile image and
  preferences.
- **Clear cached content.** Use Android's app settings for Bunny Loader and
  choose the cache-clearing option. Cached catalog content can be downloaded
  again during a later connection.
- **Clear application data or uninstall.** Android's app settings provide
  storage-clearing and uninstall options. Back up saves you want to keep
  first. These actions remove application data managed by Android; check for
  any remaining mod, save, or log folders that your device leaves behind.
- **Manage copies separately.** Imported originals, exported files,
  screenshots, clipboard contents, shared attachments, and cloud or device
  backups are separate copies. Clearing Bunny Loader's storage does not
  necessarily remove them. Use the relevant storage, application, or backup
  provider's controls.

The developer cannot remotely delete files that are stored only on your
device. For information you have shared with the project, use the contact
route below to ask about access, correction, or deletion of records under the
maintainer's control. GitHub and other providers manage their own records and
may require a separate request through their privacy controls.

## 8. Children's privacy

Parents and guardians are encouraged to help children choose mods, manage
multiplayer access, and avoid sharing personal information in public reports
or online communities. The reviewed launcher does not ask for an age or date
of birth; this alone does not establish that children cannot use it or that
third-party code processes no children's information.

If you are a parent or guardian and believe a child has shared personal
information with the project, please contact the maintainer using a short,
non-sensitive description. Please do not post the child's identifying
details in a public issue. You can also clear local application data and use
the relevant service's controls for information shared with that service.

The intended age audience, any applicable parental notice or consent process,
and the suitability of bundled code and online features for children must be
confirmed before this policy is finalized. This draft does not assert a
minimum age or claim compliance with children's privacy laws.

## 9. Changes to this policy

This policy may be updated when application features, data practices, or
applicable requirements change. Revisions will be posted in this repository,
and the date at the top will be updated. Please review the policy when you
update Bunny Loader. Any additional notice or consent required for a change
in data handling must be provided before that change takes effect; this
policy does not replace those requirements.

## 10. Contact

For privacy questions or requests, contact the Bunny Loader maintainer,
**JonataOliveiraa**, through the
[project's GitHub issues](https://github.com/JonataOliveiraa/Bunny-Loader/issues).
Use a title such as **Privacy inquiry** and begin with a brief, non-sensitive
description. Issues are public. If your request requires private information,
ask the maintainer to arrange a suitable private contact method before sending
it. Do not include passwords, private saves, or identifying information about
a child in an issue.

GitHub's account and privacy rules apply when you use GitHub to contact the
project. For security vulnerabilities, follow the separate
[Security Policy](SECURITY.md) and its private reporting instructions.

---

## Developer verification before publication

The following items are **not established by this source review**. Resolve
them, update the policy to match the verified release, and remove the draft
notice before presenting it as the final policy:

1. **Distributed code and services.** Audit the exact release APK, merged
   permissions, native libraries, bundled Terraria/Unity runtime, dependencies,
   and bundled mods. Verify network behavior, analytics, crash reporting,
   identifiers, licensing checks, and any other collection or recipients.
   Document the data categories, purposes, recipients, and retention actually
   found. Review independently downloaded executable mods as part of the
   application's ongoing data-policy responsibilities.
2. **Backups and sensitive temporary data.** Verify cloud backup and
   device-transfer inclusion rules on supported Android versions, particularly
   for `server_session.json` and its server password. Confirm storage-clearing
   and uninstall behavior for the save and mod folders. Adjust backup rules or
   implementation where needed, then reflect the resulting behavior here.
3. **Hosting and support retention.** Confirm whether the maintainer receives
   any hosting logs or keeps support information outside GitHub. Establish
   actual retention and deletion practices for developer-controlled records,
   including any retention required by applicable law. Do not invent a fixed
   retention period or assume access to GitHub's connection logs.
4. **Identity and contact.** Confirm that the responsible developer identity
   matches the store listing, that the GitHub contact route is monitored, and
   that sensitive privacy requests can be handled through an appropriate
   private channel when necessary.
5. **Children and audience.** Confirm the intended age groups and Google Play
   target-audience declaration. Review children's data handling, mod and
   multiplayer features, third-party services, and any applicable parental
   notice, consent, or Google Play Families requirements. Replace this draft's
   qualifications with an accurate children's privacy statement.
6. **Application updates.** Confirm the app-release update mechanism and its
   network requests, if any, separately from the verified catalog mod-update
   checks.
7. **Google Play publication.** Make the final policy available at an active,
   publicly accessible, non-geofenced, non-editable web URL, and link it from
   both the application and Play Console. Complete an accurate Data safety
   form covering the application and its third-party code. Implement any
   required in-app disclosures and consent. A repository document alone
   does not establish compliance.

The publication review should use Google's current
[User Data policy](https://support.google.com/googleplay/android-developer/answer/10144311?hl=en),
[Data safety guidance](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en),
and, where applicable,
[Families policies](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en).
Android's
[Auto Backup documentation](https://developer.android.com/identity/data/autobackup)
explains the platform backup behavior referenced above.
