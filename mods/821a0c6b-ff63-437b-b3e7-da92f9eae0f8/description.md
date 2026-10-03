# Info

**Wikithis** allows you to check wiki of selected item in game!

Supports multiple different languages.
---
[c/AAAAAA:(Doesn't supports mods initially, check the method below if you want to know how to add support for your mod)]

## Mod Compatibilty

> [!NOTE]
> Your mod needs to have a wiki for this to work!

You can add compatibility with your mod using the following method:
```js
export default class MyMod extends Mod {
    PostSetupContent() {
        // First, get the Wikithis mod instance
        const mod = new Ref();
        if (ModLoader.TryGetMod('wikithis', mod)) {
            // the base URL of the wiki
            const baseUrl = 'https://terrariamods.wiki.gg/wiki/MyMod/';
            // Here you would enter the type of all items that have a page on your wiki
            const itemTypes = [];
            
            // Call the method in WikiThis
            mod.value.Call(0, baseUrl, itemTypes);
        }
    }
}
```

---

The link for each item will be the baseURL + the item name, for example: `https://terrariamods.wiki.gg/wiki/MyMod/Example_Item`

**It will use the item's name in English**

---

## Replacing Entry

Sometimes the combination of URL + item name may not be sufficient; for this, you can use replace:
```js
mod.value.Call(1, ModContent.ItemType('ExampleItem'), 'https://terrariamods.wiki.gg/wiki/MyMod/CustomWikiPage')
```