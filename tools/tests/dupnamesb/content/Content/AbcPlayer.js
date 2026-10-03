export const AbcPlayer = class Abc extends ModPlayer {
    owner = 'B';
    value = 0;
    loaded = null;

    SaveData(data) {
        if (this.value) data.value = this.value;
    }

    LoadData(data) {
        this.loaded = { value: data.value };
    }
};
