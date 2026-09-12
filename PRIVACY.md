# Privacy Policy

WinForge is built with strict privacy-by-design and local-first principles.

## Data Collection & Telemetry
- **Zero Remote Telemetry:** WinForge does not collect, record, transmit, or monetize user telemetry, analytics, or behavioral tracking.
- **Local-Only Storage:** All application settings, search caches, and logs are stored strictly on the local machine under `%LOCALAPPDATA%\WingetStore\`.
- **System Optimizer Audits:** When using the System Optimizer, logs and restore points are generated locally on your workstation. No hardware identifiers or configuration exports are uploaded.

## External Network Requests
WinForge only initiates network connections when explicitly triggered by user actions:
1. **Windows Package Manager (`winget`):** Package searches and installations communicate directly with configured winget sources (e.g., Microsoft Community Repository).
2. **Package Icons & Metadata:** When viewing packages, icons may be fetched from publicly available official repository CDNs and verified endpoints.
3. **ViVeTool GitHub Releases:** When checking for or downloading ViVeTool updates, the client queries GitHub's official REST API for public release assets.

## User Rights & Data Deletion
Since WinForge does not operate remote databases or user accounts, you have full ownership of your data at all times. To delete all locally cached data and settings, simply delete `%LOCALAPPDATA%\WingetStore`.

*Last updated: 2026-09-12*
