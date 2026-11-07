plugins {
    id("com.android.asset-pack")
}

assetPack {
    packName.set("play_assets")
    dynamicDelivery {
        deliveryType.set("install-time")
    }
}
