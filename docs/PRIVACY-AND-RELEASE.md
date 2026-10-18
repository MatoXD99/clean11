# Privacy, repository hygiene, and release engineering

## Clean public repository

Start with a fresh repository containing only reviewed source and documentation. Import selected prototype files only after path, identity, dependency, and licensing review. Never import the working directory wholesale or reuse private development history.

- Use neutral repository-local commit author and committer identity. Do not change the developer's global Git configuration.
- Use synthetic fixtures, generic screenshots, and original assets. No real desktop captures, installed-app inventories, history, wallpaper configuration, device names, or diagnostic dumps in source control.
- Resolve filesystem locations through environment/known-folder APIs at runtime. Do not embed developer profile paths, network shares, machine IDs, serial numbers, or account-specific URLs.
- Keep machine state in per-user application data outside the repository. Sensitive build credentials and signing keys belong in secured CI storage.
- Keep release/build output and local caches ignored. Ignore rules are a convenience, not proof that a repository is safe to publish.
- Review Git author/committer/tag metadata, remotes, documentation, logs, image metadata, generated code, package metadata, and installer properties before publication.
- Build with deterministic path mapping and reviewed debug-symbol handling so binaries do not embed the build account's source paths. Inspect packaged binaries and symbols, not just source text.

## Runtime privacy

Core use requires no account and sends no telemetry by default. Search, ranking history, app inventory, layout, and caches stay local.

Web search, weather, notifications, clipboard history, and future integrations are separately enabled, with clear permission and retention controls. Clipboard monitoring is off by default; avoid retaining known sensitive formats, support app exclusions, short retention, clear-all, and local protected storage. Do not promise that every secret can be detected automatically.

Diagnostics exports use an allowlist of fields. Exclude query strings, window titles, file paths, clipboard content, usernames, device identifiers, and notification text by default. Show a preview before the user exports a report. Crash dumps can contain sensitive memory and are never uploaded automatically.

Theme export contains appearance only. Personal app pins, paths, launch history, and monitor layouts require a separate explicit local backup option and are not suitable as public presets.

## Source reuse and licensing

Review existing ranking and preview code for original provenance. Record the exact upstream version/commit, license, modifications, and notices for every adopted component. Keep third-party attribution required by its license; the privacy requirement concerns private project/user data and is not a reason to remove required notices.

Possible reuse candidates are existing reviewed ranking logic, window-layout logic, and optional search adapters. Do not copy an entire desktop project merely because its appearance is useful. Evaluate maintenance, API boundaries, dependency cost, and redistribution terms first.

Before public code release, select the project's own license and verify compatibility with dependencies. Produce a dependency inventory/SBOM and third-party notices. No third-party vendor logos, proprietary fonts, stock wallpapers, or reference screenshots are bundled as Clean11 assets.

## Installer design

Deliverables are `Clean11-Setup.exe` for straightforward individual installation and `Clean11.msi` for managed deployment. Installer implementation begins after the runtime/package-identity prototype.

Recommended plan:

1. Use one versioned payload and one installer-authoring source of truth. Evaluate a currently supported installer toolchain for EXE bootstrapper plus MSI, including its maintenance and license terms before selection.
2. Default to per-user installation without an always-elevated service. Offer machine-wide MSI installation when requested; administrative rights apply to installation, not ordinary UI execution.
3. Include required runtimes or acquire them through a verified installer flow. Decide self-contained versus shared-runtime deployment from size, servicing, and package-identity requirements.
4. Prototype any optional package-identity companion needed by capabilities such as notification access. MSIX is an evaluated option, not an assumed substitute for the requested EXE/MSI distribution.
5. Detect other docks/bars/desktop tools, explain conflicts, and offer module-level coexistence. Do not silently uninstall or overwrite them.
6. Onboarding selects modules, startup behavior, shortcuts, and appearance. Preview first; record native shell state before enabling desktop replacement/hiding.
7. Provide repair and uninstall entries in Windows settings. Stop only Clean11-owned processes during maintenance and restore native shell state before removing components.
8. Keep settings on uninstall by default with an explicit remove-data option. Never delete desktop documents or wallpaper files.

## Updates and signing

- Versioned releases and configuration schemas; atomic migrations with a recoverable previous version.
- Verify signed update metadata and package signatures/hashes against trusted release identity. HTTPS alone is insufficient as the full update trust model.
- User-visible update controls, release notes, and a stable/beta channel. Managed MSI installs can disable self-update for administrator deployment.
- Do not update/restart shell modules during fullscreen gaming or an active file operation. Offer a safe maintenance moment.
- Sign published installers when a suitable publisher identity is available. Certificate identity is public: choose an appropriate project/organization publisher through legitimate issuance if personal-name disclosure is unacceptable. A neutral Git author does not anonymize a signing certificate.
- If early builds are unsigned, label them accurately; do not promise warning-free installation or fabricate publisher identity.

## Public-release checklist

- Tracked-file and full-history privacy scan, plus manual review of public screenshots and example configs.
- Secret scan and private-path/identity scan; each covers different failure modes.
- Reproducible clean build with no access to a developer profile or private runtime state.
- Artifact metadata review, dependency/license inventory, release notes, known limitations, supported-build matrix.
- Clean-machine installer, upgrade, recovery, and uninstall reports.
- Review public account, repository URL, release publisher, and author identity before adding a remote or publishing.

This planning repository has no application code or installer to certify. Public publishing and signing remain later release actions.
