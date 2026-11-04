# ExampleMod, tModLoader split

tModLoader's `ExampleMod` is the only source for what `samples/ExampleModCSharp` must cover. Paths are relative to `exmod_tmod/tModLoader/ExampleMod`. Files listed here have no class of that name in the sample yet (2026-11-03). Each `ModItem`/`ModTile` file brings its texture and localization.

## Codex (131 files)

### C1, weapons and their projectiles (36)

- `Content/Items/Tools/ExampleBugNet.cs`
- `Content/Items/Tools/ExampleGolfClub.cs`
- `Content/Items/Tools/ExampleInteractableProjectileItem.cs`
- `Content/Items/Tools/ExampleMagicMirror.cs`
- `Content/Items/Weapons/ExampleAdvancedFlail.cs`
- `Content/Items/Weapons/ExampleCloneWeapon.cs`
- `Content/Items/Weapons/ExampleCustomAmmoGun.cs`
- `Content/Items/Weapons/ExampleCustomSwingSword.cs`
- `Content/Items/Weapons/ExampleCustomUseStyleWeapon.cs`
- `Content/Items/Weapons/ExampleHeldProjectileWeapon.cs`
- `Content/Items/Weapons/ExampleJavelin.cs`
- `Content/Items/Weapons/ExampleJoustingLance.cs`
- `Content/Items/Weapons/ExampleShootingSword.cs`
- `Content/Items/Weapons/ExampleSpecificAmmoGun.cs`
- `Content/Items/Weapons/ExampleStaff.cs`
- `Content/Items/Weapons/ExampleWhipAdvanced.cs`
- `Content/Items/Weapons/HitModifiersShowcase.cs`
- `Content/Projectiles/ActiveSoundShowcaseProjectile.cs`
- `Content/Projectiles/ExampleAdvancedFlailProjectile.cs`
- `Content/Projectiles/ExampleArrowProjectile.cs`
- `Content/Projectiles/ExampleCloneProjectile.cs`
- `Content/Projectiles/ExampleCustomSwingProjectile.cs`
- `Content/Projectiles/ExampleExplosive.cs`
- `Content/Projectiles/ExampleGravityDebuffBullet.cs`
- `Content/Projectiles/ExampleHeldProjectileWeaponProjectile.cs`
- `Content/Projectiles/ExampleHomingProjectile.cs`
- `Content/Projectiles/ExampleInteractableProjectile.cs`
- `Content/Projectiles/ExampleJavelinProjectile.cs`
- `Content/Projectiles/ExampleJoustingLanceProjectile.cs`
- `Content/Projectiles/ExamplePaperAirplaneProjectile.cs`
- `Content/Projectiles/ExamplePiercingProjectile.cs`
- `Content/Projectiles/ExampleWhipProjectileAdvanced.cs`
- `Content/Projectiles/Rockets/ExampleGrenadeProjectile.cs`
- `Content/Projectiles/Rockets/ExampleProximityMineProjectile.cs`
- `Content/Projectiles/Rockets/ExampleRocketProjectile.cs`
- `Content/Projectiles/Rockets/ExampleSnowmanRocketProjectile.cs`

### C2, ammo and buffs (12)

- `Content/Buffs/AbsorbTeamDamageBuff.cs`
- `Content/Buffs/AnimatedBuff.cs`
- `Content/Buffs/ExampleCrowdControlledDebuff.cs`
- `Content/Buffs/ExampleDodgeBuff.cs`
- `Content/Buffs/ExampleGravityDebuff.cs`
- `Content/Buffs/ExampleJavelinDebuff.cs`
- `Content/Buffs/ExampleWeaponImbue.cs`
- `Content/Buffs/ExampleWhipAdvancedPlayerBuff.cs`
- `Content/Items/Ammo/ExampleArrow.cs`
- `Content/Items/Ammo/ExampleCustomAmmo.cs`
- `Content/Items/Ammo/ExampleGravityDebuffBullet.cs`
- `Content/Items/Ammo/ExampleRocket.cs`

### C3, player and accessories (34)

- `Common/CustomVisualEquipType/AutoloadEquip_EarringAttribute.cs`
- `Common/CustomVisualEquipType/EarringsDrawLayer.cs`
- `Common/CustomVisualEquipType/EarringsGlobalItem.cs`
- `Common/CustomVisualEquipType/EarringsLoader.cs`
- `Common/CustomVisualEquipType/EarringsPlayer.cs`
- `Common/CustomVisualEquipType/RubyEarrings.cs`
- `Common/CustomVisualEquipType/SapphireEarrings.cs`
- `Common/ExampleCameraModifier.cs`
- `Common/Players/ExampleAccessorySlot.cs`
- `Common/Players/ExampleArmorSetBonusPlayer.cs`
- `Common/Players/ExampleBlinkingPlayer.cs`
- `Common/Players/ExampleDamageModificationPlayer.cs`
- `Common/Players/ExampleInfoDisplayPlayer.cs`
- `Common/Players/ExampleRecipeMaterialPlayer.cs`
- `Common/Players/ExampleWeaponEnchantmentPlayer.cs`
- `Common/Players/SimpleModPlayer.cs`
- `Content/BuilderToggles/ExampleBuilderToggle.cs`
- `Content/BuilderToggles/FreeBaitBuilderToggle.cs`
- `Content/CustomModType/HandsUpVictoryPose.cs`
- `Content/CustomModType/HandsUpWithFireworksVictoryPose.cs`
- `Content/CustomModType/ModVictoryPose.cs`
- `Content/CustomModType/NonAutoloadVictoryPose.cs`
- `Content/CustomModType/VictoryPoseID.cs`
- `Content/CustomModType/VictoryPoseLoader.cs`
- `Content/CustomModType/VictoryPosePlayer.cs`
- `Content/ExampleInfoDisplay.cs`
- `Content/Items/Accessories/AbsorbTeamDamageAccessory.cs`
- `Content/Items/Accessories/ExampleInfoAccessory.cs`
- `Content/Items/Accessories/ExampleMultiExtraJumpAccessory.cs`
- `Content/Items/Accessories/ExamplePotionDelayAccessory.cs`
- `Content/Items/Accessories/ExampleSpecialGlideWings.cs`
- `Content/Items/Armor/ExampleHood.cs`
- `Content/Items/Armor/ExampleTallHelmet.cs`
- `Content/Items/CameraModifierShowcase.cs`

### C4, GlobalNPC, drop rules and small systems (28)

- `Common/Commands/ExampleSummonCommand.cs`
- `Common/EntitySources/ExampleSourceDependentTweaks.cs`
- `Common/GlobalItems/ExampleResearchSorting.cs`
- `Common/GlobalItems/ShortswordGlobalItem.cs`
- `Common/GlobalItems/WeaponWithGrowingDamage.cs`
- `Common/GlobalNPCs/BuffImmuneGlobalNPC.cs`
- `Common/GlobalNPCs/DamageOverTimeGlobalNPC.cs`
- `Common/GlobalNPCs/EmotePickerGlobalNPC.cs`
- `Common/GlobalNPCs/ExampleBestiaryGlobalNPC.cs`
- `Common/GlobalNPCs/ExampleNPCHappiness.cs`
- `Common/GlobalNPCs/ExampleNPCNetSync.cs`
- `Common/GlobalNPCs/ExampleNPCShop.cs`
- `Common/GlobalNPCs/GobalNPCInteractions.cs`
- `Common/GlobalNPCs/GuideGlobalNPC.cs`
- `Common/GlobalNPCs/ProjectileModificationGlobalNPC.cs`
- `Common/GlobalProjectiles/ExampleProjectileNetSync.cs`
- `Common/GlobalProjectiles/ProjectileWithGrowingDamage.cs`
- `Common/ItemDropRules/DropConditions/ExampleJourneyModeDropCondition.cs`
- `Common/ItemDropRules/DropConditions/ExampleSoulCondition.cs`
- `Common/ManualMusicRegistrationExample.cs`
- `Common/Systems/ExampleGameTipsSystem.cs`
- `Common/Systems/LanguageSubstitutionSystem.cs`
- `Common/Systems/LanguageVariantSystem.cs`
- `Common/Systems/ModIntegrationsSystem.cs`
- `Content/Achievements/AdvancedExampleAchievement.cs`
- `Content/Currencies/ExampleCustomCurrency.cs`
- `Content/Dusts/ExampleAdvancedDust.cs`
- `Content/Dusts/ExampleBubble.cs`

### C5, other items (21)

- `Content/Items/ActiveSoundShowcase.cs`
- `Content/Items/Consumables/ExampleCanStackItem.cs`
- `Content/Items/Consumables/ExampleFlask.cs`
- `Content/Items/Consumables/ExampleFoodItem.cs`
- `Content/Items/Consumables/ExampleHealingPotion.cs`
- `Content/Items/Consumables/ExampleMultiUseItem.cs`
- `Content/Items/Consumables/PlanteraItem.cs`
- `Content/Items/CustomItemDrawingShowcase.cs`
- `Content/Items/CustomItemSets.cs`
- `Content/Items/ExampleBasicFish.cs`
- `Content/Items/ExampleDataItem.cs`
- `Content/Items/ExampleDye.cs`
- `Content/Items/ExampleHairDye.cs`
- `Content/Items/ExampleInstancedItem.cs`
- `Content/Items/ExampleOnBuyItem.cs`
- `Content/Items/ExamplePaperAirplane.cs`
- `Content/Items/ExampleResearchPresent.cs`
- `Content/Items/ExampleStackableDurabilityItem.cs`
- `Content/Items/HoldStyleShowcase.cs`
- `Content/Items/ShimmerShowcase.cs`
- `Content/Items/UseStyleShowcase.cs`

## Claude Code (137 files)

### Block 6a, furniture (27)

- `Common/GlobalItems/TorchExtractinatorGlobalItem.cs`
- `Content/Items/Placeable/ExampleCampfire.cs`
- `Content/Items/Placeable/ExampleLamp.cs`
- `Content/Items/Placeable/ExampleTorch.cs`
- `Content/Items/Placeable/ExampleWaterTorch.cs`
- `Content/Items/Placeable/Furniture/ExampleChandelier.cs`
- `Content/Items/Placeable/Furniture/ExampleClock.cs`
- `Content/Items/Placeable/Furniture/ExampleDoor.cs`
- `Content/Items/Placeable/Furniture/ExamplePlatform.cs`
- `Content/Items/Placeable/Furniture/ExampleSink.cs`
- `Content/Items/Placeable/Furniture/ExampleTable.cs`
- `Content/Items/Placeable/Furniture/ExampleToilet.cs`
- `Content/Items/Placeable/Furniture/ExampleWideBanner.cs`
- `Content/Tiles/ExampleCampfire.cs`
- `Content/Tiles/ExampleLamp.cs`
- `Content/Tiles/ExampleSink.cs`
- `Content/Tiles/ExampleTorch.cs`
- `Content/Tiles/Furniture/ExampleChandelier.cs`
- `Content/Tiles/Furniture/ExampleClock.cs`
- `Content/Tiles/Furniture/ExampleDoorClosed.cs`
- `Content/Tiles/Furniture/ExampleDoorOpen.cs`
- `Content/Tiles/Furniture/ExamplePlatform.cs`
- `Content/Tiles/Furniture/ExampleTable.cs`
- `Content/Tiles/Furniture/ExampleToilet.cs`
- `Content/Tiles/Furniture/ExampleWideBannerTile.cs`
- `Content/Tiles/Furniture/MinionBossRelic.cs`
- `Content/Tiles/Furniture/MinionBossTrophy.cs`

### Block 7, the boss (16)

- `Common/GlobalItems/BossBagLoot.cs`
- `Content/Achievements/MinionBossKilled.cs`
- `Content/BossBars/MinionBossBossBar.cs`
- `Content/BossBarStyles/ExampleBossBarStyle.cs`
- `Content/EmoteBubbles/MinionBossEmote.cs`
- `Content/Items/Armor/Vanity/MinionBossMask.cs`
- `Content/Items/Consumables/MinionBossBag.cs`
- `Content/Items/Consumables/MinionBossSummonItem.cs`
- `Content/Items/Placeable/Furniture/MinionBossRelic.cs`
- `Content/Items/Placeable/Furniture/MinionBossTrophy.cs`
- `Content/NPCs/MinionBoss/MinionBossBody.cs`
- `Content/NPCs/MinionBoss/MinionBossMinion.cs`
- `Content/Pets/MinionBossPet/MinionBossPetBuff.cs`
- `Content/Pets/MinionBossPet/MinionBossPetItem.cs`
- `Content/Pets/MinionBossPet/MinionBossPetProjectile.cs`
- `Content/Projectiles/MinionBossEye.cs`

### Block 12, NPCs (16)

- `Common/Systems/ExampleTownPetSystem.cs`
- `Common/Systems/TownNPCRespawnSystem.cs`
- `Common/Systems/TravelingMerchantSystem.cs`
- `Content/Achievements/ManyExampleWormsKilled.cs`
- `Content/EmoteBubbles/ExampleBiomeEmote.cs`
- `Content/Items/Consumables/ExampleTownPetLicense.cs`
- `Content/Items/Placeable/Banners/ExampleWormHeadBanner.cs`
- `Content/NPCs/ExampleBoneMerchant.cs`
- `Content/NPCs/ExampleCritter.cs`
- `Content/NPCs/ExampleDrawBehindNPC.cs`
- `Content/NPCs/ExampleGlobalNPC.cs`
- `Content/NPCs/ExampleTravelingMerchant.cs`
- `Content/NPCs/ExampleWorm.cs`
- `Content/NPCs/ExampleZombieThief.cs`
- `Content/NPCs/TownPets/ExampleTownPet.cs`
- `Content/NPCs/Worm.cs`

### Block 13, systems (23)

- `Common/Commands/ShowFullscreenUICommand.cs`
- `Common/Configs/CustomDataTypes/ClassUsedAsKey.cs`
- `Common/Configs/CustomDataTypes/ComplexData.cs`
- `Common/Configs/CustomDataTypes/Gradient.cs`
- `Common/Configs/CustomDataTypes/Pair.cs`
- `Common/Configs/CustomDataTypes/SimpleData.cs`
- `Common/Configs/CustomUI/CornerElement.cs`
- `Common/Configs/CustomUI/CustomFloatElement.cs`
- `Common/Configs/CustomUI/GradientElement.cs`
- `Common/Configs/ModConfigShowcases/ModConfigShowcaseAccessibility.cs`
- `Common/Configs/ModConfigShowcases/ModConfigShowcaseDataTypes.cs`
- `Common/Configs/ModConfigShowcases/ModConfigShowcaseDefaultValues.cs`
- `Common/Configs/ModConfigShowcases/ModConfigShowcaseLabels.cs`
- `Common/Configs/ModConfigShowcases/ModConfigShowcaseSubpages.cs`
- `Common/ExampleMapLayer.cs`
- `Common/Systems/ExampleWorldHeaderSystem.cs`
- `Common/Systems/SimpleDataAtParticularLocations.cs`
- `Common/UI/ExampleDisplaySets/ExampleReversedBarsDisplay.cs`
- `Common/UI/ExampleFullscreenUI/ExampleFullscreenUI.cs`
- `Common/UI/ExampleInGameNotification/ExampleInGameNotificationPlayer.cs`
- `Common/UI/ExampleInGameNotification/ExampleJoinWorldInGameNotification.cs`
- `ExampleMod.ModCalls.cs`
- `ExampleMod.Networking.cs`

### Block 6b, tiles and world (55)

- `Common/GlobalPylons/ExampleGlobalPylon.cs`
- `Common/GlobalTiles/ShakeTrees.cs`
- `Common/GlobalTiles/SunflowerChanges.cs`
- `Common/GlobalWalls/ExampleGlobalWall.cs`
- `Common/Systems/ChestItemWorldGen.cs`
- `Common/Systems/ExampleWorldGenHookingSystem.cs`
- `Common/Systems/RubbleWorldGen.cs`
- `Common/Systems/StatueWorldGen.cs`
- `Content/Items/Ammo/ExampleHellSolution.cs`
- `Content/Items/Ammo/ExampleSolution.cs`
- `Content/Items/Consumables/ExampleFishingCrate.cs`
- `Content/Items/Placeable/ExampleBlock.cs`
- `Content/Items/Placeable/ExampleCutTile.cs`
- `Content/Items/Placeable/ExampleGem.cs`
- `Content/Items/Placeable/ExampleGemsparkBlock.cs`
- `Content/Items/Placeable/ExampleHerbSeeds.cs`
- `Content/Items/Placeable/ExampleLivingFire.cs`
- `Content/Items/Placeable/ExamplePressurePlate.cs`
- `Content/Items/Placeable/ExamplePylonItemAdvanced.cs`
- `Content/Items/Placeable/ExampleSandBlock.cs`
- `Content/Items/Placeable/ExampleSlopedTile.cs`
- `Content/Items/Placeable/ExampleStatue.cs`
- `Content/Items/Placeable/ExampleTrap.cs`
- `Content/Items/Placeable/ExampleWall.cs`
- `Content/Items/Placeable/ExampleWallAdvanced.cs`
- `Content/Items/Tools/ExampleSandRod.cs`
- `Content/Items/Weapons/ExampleExplosive.cs`
- `Content/Projectiles/ExampleSandBallProjectile.cs`
- `Content/TileEntities/AdvancedPylonTileEntity.cs`
- `Content/Tiles/Example1x1Rubble.cs`
- `Content/Tiles/Example2x1Rubble.cs`
- `Content/Tiles/Example3x2Rubble.cs`
- `Content/Tiles/ExampleAnimatedGlowmaskTile.cs`
- `Content/Tiles/ExampleAnimatedTile.cs`
- `Content/Tiles/ExampleCritterCage.cs`
- `Content/Tiles/ExampleCustomFramingTile.cs`
- `Content/Tiles/ExampleCutTile.cs`
- `Content/Tiles/ExampleExposedGem.cs`
- `Content/Tiles/ExampleFishingCrate.cs`
- `Content/Tiles/ExampleGemsparkBlock.cs`
- `Content/Tiles/ExampleHerb.cs`
- `Content/Tiles/ExampleLivingFireTile.cs`
- `Content/Tiles/ExamplePressurePlate.cs`
- `Content/Tiles/ExamplePylonTileAdvanced.cs`
- `Content/Tiles/ExampleSand.cs`
- `Content/Tiles/ExampleSlopeTile.cs`
- `Content/Tiles/ExampleStatue.cs`
- `Content/Tiles/ExampleTransparentShapedTile.cs`
- `Content/Tiles/ExampleTrap.cs`
- `Content/Tiles/ExampleVanillaConversionTiles.cs`
- `Content/Tiles/Plants/ExampleCactus.cs`
- `Content/Tiles/Plants/ExamplePalmTree.cs`
- `Content/Tiles/Plants/ExampleVine.cs`
- `Content/Tiles/TileObjectDataShowcase.cs`
- `Content/Walls/ExampleVanillaConversionWalls.cs`
