# Character Creation Fix Plugin

Static audit against the original PC client (2026-07-18):

- `Gateway(GameServer, WorldContext, PlayerContext)` is the correct constructor to wrap.
- the original `/players` POST route saves appearance, region, starter clothes and player context before returning HTTP 200;
- the wrapper then initializes the chosen profession's configured skill categories through `SkillSystemApi`, which persists its own skill state;
- `FullScreenMovieGroupBase.Play(string, bool, Action)` is the correct static movie entry point;
- `StreamingAssets/Movie/PC/Durango-Wild-Lands-Opening-Movie.asset` contains an
  ordinary MP4 stream with a valid `ftyp` header.

Version 0.1.7 passes `Movie/PC/Durango-Wild-Lands-Opening-Movie.asset` directly
to the native movie player without copying, renaming, decrypting, or using HTTP.
While that opening movie is active, its `MovieTexture` uses NGUI `FitOutside`
(Cover) so 16:9 video fills Ultrawide windows and excess image area is cropped.
The original layout is restored when playback stops, leaving other movies unchanged.
It also clamps the submitted profession to the same 0-7 range used by the
original route. The SkillSystem dependency remains reflection-based and fails
safely if that plugin is absent.
