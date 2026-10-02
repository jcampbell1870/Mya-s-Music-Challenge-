# Songs folder

Each song gets its own folder named after the song id shown in the game (for example `its-all-about-me`):

```
Songs/
  its-all-about-me/
    lyrics.lrc     synced lyrics in LRC format: [00:12.50]First line of the song
    backing.mp3    instrumental / karaoke backing track (.mp3, .wav, .m4a, .wma or .aac)
    melody.json    optional vocal guide: [{ "start": 12.5, "duration": 0.4, "midi": 64 }, ...]
  my-custom-song/
    song.json      add a song that isn't in the catalog: { "title": "...", "album": "...", "year": 2003, "spotifyTrackId": "..." }
    lyrics.lrc
```

Lyrics and recordings are copyrighted, so the game does not ship them. Only use files you are licensed to use
(for example karaoke tracks you have purchased).
