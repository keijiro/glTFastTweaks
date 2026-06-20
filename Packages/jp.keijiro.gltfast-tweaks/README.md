# glTFast Tweaks

**glTFast Tweaks** is a Unity Editor extension that customizes how the
[glTFast] importer handles textures, letting you override texture import
settings per glTF asset directly from Project Settings.

[glTFast]:
  https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.19/manual/index.html

## Features

### Texture Overrides

When a `.glb`/`.gltf` asset is imported, its embedded textures are rewritten
according to a set of override options:

- **Size** -- longest-edge clamp (downscale)
- **Compression** -- GPU (BC) compression level
- **Filter** -- texture filtering mode

Each imported asset gets a row in the settings table. A **Defaults** row at the
top is applied to every asset whose own switch is off, so you only need to set
explicit values for the assets that differ.

The settings live in **Project Settings &gt; glTFast Tweaks**.

## System Requirements

- Unity 6 or later
- glTFast (`com.unity.cloud.gltfast`)

## How to Install

The glTFast Tweaks package (`jp.keijiro.gltfast-tweaks`) can be installed via
the "Keijiro" scoped registry using Package Manager. To add the registry to
your project, please follow [these instructions].

[these instructions]:
  https://gist.github.com/keijiro/f8c7e8ff29bfe63d86b888901b82644c
