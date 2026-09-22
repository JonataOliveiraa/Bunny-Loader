plugins {
    id("com.android.asset-pack")
}

assetPack {
    packName.set("terraria1456_assets")
    dynamicDelivery {
        deliveryType.set("install-time")
    }
}
