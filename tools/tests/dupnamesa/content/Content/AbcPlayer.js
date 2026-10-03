export const AbcPlayer = class Abc extends ModPlayer {
    owner = 'A';
    value = 0;
    loaded = null;

    SaveData(data) {
        if (this.value) data.value = this.value;
    }

    LoadData(data) {
        this.loaded = { value: data.value };
    }
};
