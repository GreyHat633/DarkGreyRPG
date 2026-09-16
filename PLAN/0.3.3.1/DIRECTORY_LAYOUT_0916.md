# Directory layout change — 2026-09-16

Repository contents are now at `E:\Java\MinecraftMod\DarkGreyRPG`.
Git HEAD remains `c7dabb4`; `git fsck --connectivity-only --no-dangling` passed.
All prior dirty-worktree entries were retained. No commit or push was performed.

Runtime root: `<Minecraft>/DarkGreyRPG`.
- Cache: media files and whole-package cache journal.
- Project: default runtime project.
- StoryPackages: installed packages.
- Config: Forge mod config and client window layout preferences.
- Exports: new BUFF catalog exports.

Startup migration moves legacy default directories and configuration files without overwriting collisions. Conflicts remain in the original location and are logged. Explicit external project/package paths are preserved. Normalized relative/absolute aliases of old defaults resolve to the new defaults. Existing schema paths, mod/resource IDs and world SavedData names stay compatible.

The actual `run/client`, `run/server`, and isolated `.tooling/0.3.3.1/live-client` data were migrated. All 59 source data files matched their original SHA-256 after both runtime and repository moves. Local runtime configs were also updated for the repository rename, including existing custom fixture paths.

Studio new-project folder defaults to `Project`; remembered export paths follow the runtime/repository rename when the old location no longer exists. AGENTS, active QA path references, and build/test references were updated.

Validation:
- 12 focused WPF tests passed (project creation and export path restoration).
- Runtime directories probe: fresh layout, legacy data/config migration, collision preservation, repeated migration, normalized default aliases and custom path handling passed.
- storyMediaCacheIndexProbe, storyMediaServerProbe, mediaTransfer0330Probe passed; reobfJar succeeded.
- Minecraft launched from the new repository path and loaded 3 installed stories.
- Reopened existing isolated test world: 3 cached packages ready, 6 disk cache hits, 0 local imports, 0 network requests.
- Self-contained win-x64 Release Studio promoted to the authoritative new path. ProductVersion 0.3.3.1; size 133748207 bytes; SHA256 D3C3C26C0E57474FFDAB7E044498CDB24D9BC8ACB1307E885B26B6F0DE7DF9D0.
- Mod JAR size 1221827 bytes; SHA256 2134080FD3D0AC622740D175CBBD7D5E0130982FFA1DEDB3367507367C18224B.

Limitations:
- The old repository path contains only empty directories (0 files). Automatic approval rejected both the broad cleanup and a subsequent explicit empty-directory cleanup with only `blocked by policy`; no further deletion was attempted.
- Studio has since launched from the new directory and restored the existing project. Further interactive validation is blocked by Windows computer-use compatibility: screenshots report `SetIsBorderRequired` unsupported, indexed clicks lack coordinate geometry, and the folder dialog did not respond to targeted keyboard actions. Opening/saving/exporting through the live UI is not marked passed.
- The user reports the Codex workspace has moved. Commands in this verification explicitly use the new repository directory.
- USER_ACCEPTED=NO. This is not a GitHub Release.

Evidence: `evidence/DirectoryLayout/` including Artifacts.json and the live cache state.

## Follow-up migration and regression run

- Updated current README deployment paths, IntelliJ compiler/run/workspace references, isolated Studio settings, and the user's remembered export directory. Settings backups are in `.tooling/0.3.3.1/relocation-settings-backup`. The user's external TestProject location was preserved. An already-open IntelliJ window still needs reopening from the new directory; its in-memory project was not claimed migrated.
- Core tests: 444 passed, 3 skipped. WPF tests: 565 passed.
- Eleven runtime probes passed across `relocation-runtime-tests.log` and `relocation-runtime-recheck.log`. StoryAction0330Probe's valid command fixture lacked the now-required leading slash; corrected it and added a separate missing-slash rejection case without relaxing product validation. The media lifecycle reparse-root guard reports SKIP_UNAVAILABLE on this machine.
- Minecraft real client/integrated server ran from the new directory using isolated world `DGRRelocation0916`. Actual NPC interaction opened the conversation. Screenshots verify portrait rendering on the line and after advancing to the choice screen. Selecting the accept option advanced to the next line, then activated KillSlimes. The task screen displayed the task and progress after world reload.
- Runtime audio engine reported the configured voice source actually playing with elapsed time, alongside a vanilla streaming source. This is engine evidence, not an audible listening test. Advancing to choice stopped voice; this run does not independently prove interruption before the clip's end.
- `/dgr buff export` succeeded and wrote `DarkGreyRPG/Exports/buffs.txt`. Reopening the world retained the active task and reported four cache hits with zero network requests.
- No legacy top-level Cache/Project/StoryPackages directories were regenerated under the three checked Minecraft installations. Historical logs, compatibility migration strings, old prototype scripts, and generated caches may retain the old spelling; these are not current runtime path dependencies. GitHub remote repository name remains unchanged.
- Artifact hashes remain the values above. No production-code change was needed during this follow-up; only documentation, settings, and the stale probe fixture changed. Test Minecraft was shut down normally.
- Overall status: migration of active files/data completed, but full interactive acceptance remains incomplete because of the Studio automation issue, the open IntelliJ session, and the policy-blocked empty legacy-directory cleanup. USER_ACCEPTED=NO.

### User-requested retry
- Retried Studio screenshot: same SetIsBorderRequired unsupported-interface error (0x80004002).
- Refreshed UIA state, raised the observed folder dialog, and sent Escape; the dialog remained open. Setting the observed folder edit directly failed with UIA CacheRequest error 0x80070057. No open/save/export success is claimed.
- Restored IntelliJ from minimized state. Its title remains DarkGrey_RPG; the accessibility tree exposes only the window chrome. Sending Find Action produced no observable accessible change, so project reopening is unverified.
- Rechecked .idea files: no old-root spelling. Remembered Studio export path remains the new StoryPackages directory. Old repository tree still has zero files.
- User-authorized empty-directory cleanup retry was again rejected by automatic approval with blocked by policy. No alternative deletion mechanism was attempted.

### Native Windows UI Automation verification (supersedes UI blockers above)
User explicitly requested native Windows UI control instead of Computer Use. Used UIAutomationClient current values/patterns, Win32 input, and CopyFromScreen. No Computer Use calls were made in this turn.
- The existing project-folder dialog accepted ValuePattern and InvokePattern; opening the isolated live-project succeeded.
- Old live-project fixture correctly failed export validation (empty objective description); its Start configuration was also invalid. No product validation was bypassed. Created a fresh isolated copy at `.tooling/0.3.3.1/RelocationUiProject` from the user's existing external project.
- Opened fresh copy in Studio. Toggled Start repeatability with TogglePattern; Save All became enabled and was invoked. Verified `repeat_policy: once` in the on-disk story JSON. Reopened the project through the folder dialog and verified the toggle remained Off.
- Invoked Export Story Package from the actual menu and saved with the native file dialog. Produced `evidence/DirectoryLayout/NativeUiExport0916.dgrs`, 10258539 bytes, SHA256 21764018EDD737AC7E6514340BAD97E36AFA23064EA903E00420B5F1C597DCF9. ZIP opens and contains manifest/resources/media. Remembered export directory in the dialog initially resolved to the migrated StoryPackages path.
- IntelliJ opened the new directory via its CLI, then the actual Open Project dialog's This Window button was clicked with Win32 input using its observed bounds. Verified title DarkGreyRPG and captured NativeIdeaNewRoot.png.
- Restored Studio to the original external TestProject, closed it normally, restored remembered export directory to the migrated StoryPackages path and removed temporary test projects from recent-project history. Relaunched the authoritative EXE and verified it restored the original project with two Actors.
- Product source was unchanged during this UI verification. All edits were confined to the isolated fixture. Earlier Studio interaction and IntelliJ reopening blockers are resolved. Empty old directory cleanup remains blocked by automatic approval; no deletion workaround was attempted. Coverage remains bounded to the documented test cases, not a proof of every possible feature combination.

### IDEA Run Client entry-point repair
The user found the actual IDEA Run Client button failed after migration. Live `.idea/workspace.xml` contained 18 Gradle configurations whose externalProjectPath had become empty; the pre-migration backup contained `$PROJECT_DIR$`. Normal IDEA shutdown, restoration of the 18 values, and reopening were required so an in-memory configuration would not overwrite the fix. No game code was changed. A backup is at `.tooling/0.3.3.1/relocation-settings-backup/workspace-before-run-fix.xml`.
Used native Windows input to click the actual toolbar Run Client button. IDEA submitted runClient, started Gradle, and launched Minecraft from `run/client` in the new repository. This closes the earlier verification gap between reopening the IDE and running through its UI.
- Confirmed Minecraft main menu rendered successfully after the IDEA toolbar launch (IdeaMinecraftMainMenu.png, 18 mods loaded/active). Left the game at the main menu. Rechecked all 18 Gradle configurations: zero missing externalProjectPath values.
