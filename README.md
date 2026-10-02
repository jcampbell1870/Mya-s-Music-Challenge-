# Mýa's Music Challenge

Mýa's Music Challenge is a 2D karaoke game for Windows PCs. Mýa (Mýa Harrison) is the main character. Pick one of her songs, sing it into your microphone, and when the song ends Mýa gives you a **professional singing assessment**. Every completed performance also earns **Arcade1870 (A1870)** tokens, paid from the **same reward treasury used by [Crypto Hockey](https://github.com/jcampbell1870/Crypto-Hockey)**.

**Play in Chrome or download:** [Open the GitHub Pages game](https://jcampbell1870.github.io/Mya-s-Music-Challenge-/) · [Windows PC download](https://github.com/jcampbell1870/Mya-s-Music-Challenge-/releases/latest/download/MyaMusicChallenge-Windows-x64.zip) · [Chromebook download](https://github.com/jcampbell1870/Mya-s-Music-Challenge-/releases/latest/download/MyaMusicChallenge-Chromebook.zip)

The Chromebook/browser edition is an installable, microphone-powered vocal warm-up and pitch challenge. Open the Pages link in Chrome and choose **Install game** (or Chrome menu → **Install page as app**). Its audio analysis runs locally in your browser. The Windows edition is the full desktop karaoke game. The project does not include copyrighted songs, lyrics, or backing tracks.

## Features

- **The Mýa stage show.** Mýa is drawn on a concert stage with spotlights, a light show and a crowd. She sings along while you perform, talks you through your results, and celebrates big scores.
- **Karaoke screen.**
  - Synced lyrics that highlight as the line is sung.
  - A scrolling pitch lane showing your voice, plus guide notes if a melody file is installed.
  - Live PERFECT / GREAT / GOOD feedback and a mic level meter.
- **Professional vocal assessment.** Your performance is scored in six categories:

  | Category | Weight |
  |---|---|
  | Pitch Accuracy | 30% |
  | Pitch Stability | 15% |
  | Rhythm & Timing | 20% |
  | Vocal Range | 10% |
  | Dynamics Control | 10% |
  | Breath Support | 15% |

  - Detected vibrato earns a small bonus.
  - You also get vocal stats: range, detected key, average cents off pitch, longest phrase and vibrato.
  - Mýa gives her verdict in a speech bubble: strengths, things to work on, a coaching tip and a grade from **S Superstar** to **E Studio Rookie**.
- **All of Mýa's songs.**
  - A built-in catalog of her signature songs is always available.
  - Press **F5** to sync her full discography (albums and singles) from Spotify.
  - Each song can be opened in the Spotify app with **S**.
  - You can add any other song with a `song.json` file.
- **A1870 rewards just for playing.** Sing for at least 30 seconds and you earn 10 A1870, whatever your score.

## Requirements

- Windows 10/11 with a microphone. Headphones are recommended so the mic only hears your voice and not the backing track.
- Chromebook: Chrome browser and microphone permission for the secure GitHub Pages site. Installing the site as a Chrome app is optional.
- The [.NET 10 SDK](https://dotnet.microsoft.com/download) to build from source.

## Build & run

```powershell
dotnet run --project src/MyasMusicChallenge
```

To build a self-contained Windows executable:

```powershell
dotnet publish src/MyasMusicChallenge -c Release -r win-x64 --self-contained
```

### Tests

The game logic lives in `src/MyasMusicChallenge.Core` and runs on any OS. This covers pitch detection, the assessment, the song library and LRC lyrics, the Spotify client, and reward encoding and validation. Run its tests with:

```bash
dotnet test tests/MyasMusicChallenge.Tests
```

## Controls

| Screen | Keys |
| --- | --- |
| Title | **Enter** start · **Esc** quit · **F11** full screen |
| Song select | **↑/↓/PgUp/PgDn** choose · **Enter** sing · **S** open in Spotify · **F5** sync Spotify discography · **W** set wallet · **N** set name · **M** switch microphone · **O** open songs folder |
| Singing | **Space** start (when there is no backing track) · **Enter** finish and get Mýa's assessment · **Esc** quit song |
| Results | **Space/→** Mýa's next comment · **C** claim A1870 · **W** set wallet · **R** retry reward · **A** sing again · **Enter** back to songs |

## Adding karaoke files (lyrics, backing tracks)

Song lyrics and recordings are copyrighted, so the game does not include them.

1. Find the song's id: it is shown on the song select screen, for example `Songs\its-all-about-me\`.
2. Put your licensed files in that folder inside the game's `Songs` folder:
   - `lyrics.lrc`: synced lyrics in [LRC](https://en.wikipedia.org/wiki/LRC_(file_format)) format.
   - `backing.mp3` (or `.wav`, `.m4a`, `.wma`, `.aac`): the instrumental. When this file exists, the music, lyrics and microphone all start together after the countdown.
   - `melody.json` (optional): a vocal guide in the form `[{ "start": 12.5, "duration": 0.4, "midi": 64 }]`. It shows target notes on the pitch lane and makes pitch and timing scoring stricter.
   - `song.json` (optional, for songs that aren't in the catalog): `{ "title": "…", "album": "…", "year": 2003, "spotifyTrackId": "…" }`.

If a song has no backing track:
1. Press **S** to open it in Spotify and start playback.
2. Press **Space** as the song begins.

You can also skip Spotify and sing a cappella.

See [`src/MyasMusicChallenge/Songs/README.md`](src/MyasMusicChallenge/Songs/README.md) for the full folder format.

## Spotify (optional)

1. Create an app at the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard).
   - Since February 2026, development-mode apps need the app owner to have Spotify Premium.
2. Give the game your Client ID and Secret using **one** of these:
   - Environment variables (recommended): `MYA_SPOTIFY_CLIENT_ID` and `MYA_SPOTIFY_CLIENT_SECRET`.
   - The `Spotify` section of `src/MyasMusicChallenge/appsettings.json`. Never commit real secrets.
3. Press **F5** on the song select screen.

The game finds Mýa's artist profile, reads all her albums and singles, and saves the track list (with Spotify links) to your player profile.

## Arcade1870 rewards (shared with Crypto Hockey)

The `Rewards` section of `appsettings.json` defaults to the same Arcade1870 treasury that Crypto Hockey uses:

| Setting | Value |
| --- | --- |
| A1870 token | `0x8eddD4edea39c5B5f77662453600F53A202EE47C` |
| Reward vault (`Arcade1870RewardVault`) | `0x1e4f6e4a382adbdb662733a19ae773d3ab8f497d` |
| Reward issuer | `https://www.cryptohockey.org/api/reward-claim` |
| Reward per performance | `10` A1870 (18 decimals) |
| Chains | Ethereum mainnet (1), Sepolia (11155111), Polygon (137) |

How a reward is paid:

1. Set your wallet address with **W**.
2. After each performance of at least 30 seconds, the game sends a unique game proof to the shared reward issuer, using the same `POST /api/reward-claim` contract as Crypto Hockey. The proof is: `gameId` `mya-<song>-<guid>`, mode `mya-karaoke`, your score, and `playerWon: true`, because you earn tokens just for playing.
3. The issuer returns a signed EIP-712 claim. The game checks it against the configured vault, chain and deadline.
4. Press **C**. The game serves a claim page on `127.0.0.1` and opens it in your browser. MetaMask then:
   - connects your wallet,
   - switches to the right network,
   - sends `claim(amount, nonce, deadline, signature)` to the vault,
   - offers to add A1870 to your wallet.

Each game proof can only be claimed once. Your profile, wallet and score history are stored in `%APPDATA%\MyasMusicChallenge\player.json`.

## Project layout

```
MyasMusicChallenge.slnx
src/MyasMusicChallenge.Core/   cross-platform logic
  Audio/        YIN pitch detector and real-time vocal analyzer
  Assessment/   scoring engine and Mýa's feedback
  Songs/        Mýa catalog, Songs folder library
  Lyrics/       LRC parser
  Rewards/      Arcade1870 issuer client, claim encoder, MetaMask claim page
  Spotify/      Spotify Web API discography sync
  Configuration/ appsettings + player profile
src/MyasMusicChallenge/        Windows game (WinForms + NAudio)
  Scenes/       Title, SongSelect, Sing, Results
  Rendering/    stage, Mýa character, UI helpers
  Audio/        microphone capture, backing-track playback
tests/MyasMusicChallenge.Tests/  xUnit tests
docs/                             GitHub Pages browser game and installable PWA
```

## Publishing downloads

GitHub Pages deploys the browser game from `docs/` when changes reach `main` or `master`. The public Pages URL is [jcampbell1870.github.io/Mya-s-Music-Challenge-/](https://jcampbell1870.github.io/Mya-s-Music-Challenge-/). Set **Settings → Pages → Build and deployment → Source** to **GitHub Actions** once to enable deployment. Each update to `main` or `master` publishes Windows and Chromebook ZIP downloads; pushing a `v*` version tag publishes a named release. The download buttons above always point to the latest release.

*Mýa's Music Challenge is a fan-made game and is not affiliated with Mýa, her labels or Spotify.*
