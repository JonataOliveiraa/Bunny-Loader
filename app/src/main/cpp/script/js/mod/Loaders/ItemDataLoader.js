// O arquivo nativo .plr.bl/.wld.bl guarda o JSON junto da identidade estavel
// do item: remapeamento de tipos e realocacao de slots preservam os dados.
class ItemDataLoader {
    static Encode(tag) {
        const visiting = new Set();
        const visit = (value, depth = 0) => {
            if (depth > 128) throw new TypeError('SaveData: limite de profundidade excedido');
            if (value === null || typeof value === 'string' || typeof value === 'boolean') return;
            if (typeof value === 'number' && Number.isFinite(value)) return;
            if (!value || typeof value !== 'object') throw new TypeError('SaveData: valor nao serializavel');

            const prototype = Object.getPrototypeOf(value);
            if (!Array.isArray(value) && prototype !== Object.prototype && prototype !== TagCompound.prototype && prototype !== null) {
                throw new TypeError('SaveData: use valores, listas e TagCompound; nao objetos do jogo');
            }
            if (visiting.has(value)) throw new TypeError('SaveData: referencia circular');
            visiting.add(value);

            if (Object.getOwnPropertySymbols(value).length) throw new TypeError('SaveData: chave Symbol nao serializavel');
            if (Array.isArray(value) && Object.keys(value).length !== value.length) throw new TypeError('SaveData: lista esparsa ou com propriedades extras');

            for (const key of Object.keys(value)) {
                const descriptor = Object.getOwnPropertyDescriptor(value, key);
                if (!('value' in descriptor)) throw new TypeError('SaveData: getter nao serializavel');
                visit(descriptor.value, depth + 1);
            }

            for (const key of Object.getOwnPropertyNames(value)) {
                if (Array.isArray(value) && key === 'length') continue;
                if (!Object.getOwnPropertyDescriptor(value, key).enumerable) throw new TypeError('SaveData: propriedade nao enumeravel');
            }

            visiting.delete(value);
        };

        visit(tag);

        return JSON.stringify(tag);
    }

    static Save(item) {
        if (item.__blItemDataLoadFailed) throw new Error('SaveData: LoadData falhou; dados anteriores preservados');

        const m = ItemLoader.Of(item);
        if (!m || !Hooks.Overrides(m.constructor, ModItem, 'SaveData')) return item.__blItemData || '';

        const tag = new TagCompound();
        m.SaveData(tag);

        const data = Object.keys(tag).length ? ItemDataLoader.Encode(tag) : '';
        item.__blItemData = data;

        return data;
    }

    static Load(item, text) {
        item.__blItemData = text || '';
        item.__blItemDataLoadFailed = true;

        const data = text ? JSON.parse(text) : {};
        if (!data || typeof data !== 'object' || Array.isArray(data)) throw new TypeError('LoadData: TagCompound invalida');
        ItemDataLoader.Encode(data);

        const m = ItemLoader.Of(item);
        if (m && Hooks.Overrides(m.constructor, ModItem, 'LoadData')) m.LoadData(TagCompound.from(data));

        item.__blItemDataLoadFailed = false;
    }

    static Copy(source, target) {
        if (!source || !target || source === target) return;

        target.__blItemData = source.__blItemData;
        target.__blItemDataLoadFailed = source.__blItemDataLoadFailed;

        const m = ItemLoader.Of(source);
        if (m) Entities.Bind(m.Clone(target), target, 'ModItem');
    }

    static Install(cls) {
        if (['SaveData', 'LoadData', 'NetSend', 'NetReceive'].some(name => Hooks.Overrides(cls, ModItem, name))) {
            Hooks.Once('item.DataTransfers', ItemDataLoader.InstallTransfers);
            ItemNetworkHooks.Install();
        }
    }

    static InstallTransfers() {
        const Item = Terraria.Item;

        Item['Item DeepClone()'].hook(
            (original, self) => {
                const copy = original(self);
                Safe.Run('ModItem.DeepClone', () => ItemDataLoader.Copy(self, copy));

                return copy;
            },
            { minType: FIRST_ITEM, on: -1 }
        );

        Item['Item clientClone(Item cloneDestination)'].hook(
            (original, self, destination) => {
                const copy = original(self, destination);
                Safe.Run('ModItem.clientClone', () => ItemDataLoader.Copy(self, copy));

                return copy;
            },
            { minType: FIRST_ITEM, on: -1 }
        );
    }
}

Entities.Define(Terraria.Item, '__blItemData');
Entities.Define(Terraria.Item, '__blItemDataLoadFailed');
