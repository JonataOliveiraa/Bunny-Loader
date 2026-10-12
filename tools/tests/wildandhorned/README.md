# Wild and Horned tests

The suite uses the Wild and Horned package's code. When it is not in `samples/WildAndHorned`, extract the desired version of the `.bl` into a temporary folder and point to the root that contains `content/`:

```powershell
$env:BL_WILDANDHORNED_ROOT = 'C:/Scripts/Bunny Loader/build/wildandhorned/github-validation'
node --experimental-vm-modules tools/tests/wildandhorned/check.mjs
```

The published package is in the [Wild and Horned releases](https://github.com/JonataOliveiraa/Bunny-Loader/releases/tag/mod-wildandhorned-v1.5.2). The suite checks behavior in a controlled JavaScript environment. `content/main.js` is the separate in-game fixture, depends on the installed mod and logs `wildandhorned-qa` results; automated runs do not replace validation on the device.
