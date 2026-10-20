# V035 display lettering

Game titles now use the original **Ma Shan Zheng** brush-script face from the Google Fonts repository. It has visibly different brush forms from the existing WenKai-derived heading font. Body instructions retain Tide Sans SC for reading at small sizes.

`source.json` pins the upstream commit, download URL and SHA256. The font is unmodified and redistributed under the adjacent SIL OFL 1.1 license. The previous Tide Hand SC asset remains as a CJK fallback in Unity's font importer; it is not overwritten.

The gilt treatment is rendered on the glyph silhouettes, using eight small text offsets for bronze/gold/highlight edges and a final separately coloured glyph interior. It does not use panel borders to simulate lettering. Day/night-aware teal, blue, jade, rose and violet fills distinguish home and the four realms. Actual game screenshots, rather than font previews, document the result in `Docs/VALIDATION_V035.md`.
