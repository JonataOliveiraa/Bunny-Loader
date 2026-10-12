# _icons/ — source textures

This is where the files the app's resources **come from** live, at their
original resolution and crop. The app reads nothing from here: what it uses is
`app/src/main/res/drawable-nodpi/`, where the texture is copied when it
actually starts being used.

The two sides name things by different criteria, on purpose:

- **here, by what the image IS** (`arvore.png`, `nuvem_coelho.png`), because
  the material outlives the screen that uses it;
- **in `res/`, by ROLE** (`ic_tab_inicio`, `ic_start`), because that is how
  the code asks for it.

That is what was missing when the Profile tab became Settings and the resource
stayed `ic_tab_perfil`: the role changed, the name did not.

```
launcher/   launcher UI: tabs, buttons, title
menu/       in-game cheat menu
sprites/    animated things: bunny, coin, sunflower
tiles/      block atlases (18x18: 16px tile + 2px border)
fonte/      the pixel font
```

## This folder does not go into git

The content is Terraria art, extracted from the game's assets. It is in
`.gitignore` — only this README is versioned. What is already in `res/` got in
before this rule existed.
