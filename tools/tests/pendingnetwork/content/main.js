import { role } from './config.js';
const Main = Terraria.Main, B = Terraria.GameContent.BannerSystem;
let mod, ticks = 0, started = false, hosting = false, remote = -1, phase = '', due = 0, checks = 0, failures = 0;
let modified = [], notified = [], hurt = [], sent = 0, helloCount = 0, claimedBefore = 0;
let packetsSeen = 0;
Terraria.MessageBuffer['void ProcessData(byte[] messageData, int length, out int messageType)'].hook((original, self, data, length, type) => {
    if (packetsSeen++ < 45) bl.log('pendingnetwork TRACE packet=' + data[0] + ' from=' + self.whoAmI + ' mode=' + Main.netMode);
    return original(self, data, length, type);
});
function check(name, ok) { checks++; if (!ok) failures++; bl.log('pendingnetwork ' + role + ' ' + (ok ? 'PASS ' : 'FAIL ') + name); }
function send(kind, values = [], to = -1) { const p = mod.GetPacket(); p.Write(kind); for (const v of values) p.Write(v); p.Send(to); }
function banner() { return ModContent.NPCType(Enemy); }
function itemType() { return ModContent.ItemType(Banner); }
function totalItems() {
    let count = 0; const player = Main.player[Main.myPlayer];
    for (let i = 0; i < player.inventory.length; i++) if (player.inventory[i].type === itemType()) count += player.inventory[i].stack;
    if (Main.mouseItem.type === itemType()) count += Main.mouseItem.stack;
    for (let i = 0; i < Main.item.length; i++) if (Main.item[i].type === itemType() && Main.item[i].active) count += Main.item[i].stack;
    return count;
}
export class Weapon extends ModItem {
    Texture = 'Box'; HideFromModMenu = true;
    SetDefaults(item) { item.width = item.height = 16; item.damage = 10; item.melee = true; item.useStyle = 1; this.power = 3; this.allowed = true; }
    CanHitPvp() { return this.allowed; }
    ModifyHitPvp(item, player, target, modifiers) { modified.push({ from: player.whoAmI, target: target.whoAmI, power: this.power }); modifiers.damage = this.power; modifiers.crit = false; }
    OnHitPvp(item, player, target, info) { notified.push({ from: player.whoAmI, target: target.whoAmI, damage: info.Damage }); }
    NetSend(writer) { writer.Write(this.power); }
    NetReceive(reader) { this.power = reader.ReadInt32(); }
}
export class Banner extends ModItem {
    Texture = 'Banner'; HideFromModMenu = true;
    SetDefaults() { this.DefaultToPlaceableTile(ModContent.TileType(BannerTile)); }
}
export class BannerTile extends ModBannerTile { Texture = 'BannerTile'; }
export class Enemy extends ModNPC {
    Texture = 'Slime'; HideFromModMenu = true; HideFromBestiary = true;
    SetDefaults() { this.CloneDefaults(Terraria.ID.NPCID.BlueSlime); this.Banner = this.Type; this.BannerItem = ModContent.ItemType(Banner); }
}
export class Observer extends ModPlayer {
    OnHurt(player, source, damage, direction, pvp) { if (pvp && player.whoAmI === Main.myPlayer) hurt.push({ from: source._sourcePlayerIndex, damage }); }
}
const gate = Terraria.Player['void ItemCheck_MeleeHitPVP(Item sItem, Rectangle itemRectangle, int damage, float knockBack)'];
gate.hook((original, player, item, rect, damage, knockback) => original(player, item, rect, damage, knockback));
Terraria.NetMessage['void SendPlayerHurt(int playerTargetIndex, PlayerDeathReason reason, int damage, int direction, bool critical, bool pvp, int hitContext, int remoteClient, int ignoreClient)'].hook(
    (original, target, source, damage, direction, crit, pvp, context, remote, ignore) => {
        if (pvp) { sent++; bl.log('pendingnetwork SEND local=' + Main.myPlayer + ' from=' + source._sourcePlayerIndex + ' target=' + target + ' damage=' + damage); }
        return original(target, source, damage, direction, crit, pvp, context, remote, ignore);
    }, { whileIn: gate });
function reset() { modified = []; notified = []; hurt = []; sent = 0; }
function preparePlayers() {
    for (const index of [Main.myPlayer, remote]) {
        const p = Main.player[index]; if (!p || !p.active) continue;
        p.hostile = true; p.team = 0; p.immune = false; p.immuneTime = 0; p.statDefense = 0; p.endurance = 0;
        p.hurtCooldowns[0] = p.hurtCooldowns[1] = 0; p.statLife = p.statLifeMax2;
    }
}
function attack(allow) {
    preparePlayers(); reset();
    const player = Main.player[Main.myPlayer], target = Main.player[remote];
    const weapon = Terraria.Item.new(); weapon['void .ctor()'](); weapon['void SetDefaults(int Type, ItemVariant variant)'](ModContent.ItemType(Weapon), null);
    weapon.ModItem.power = 7; weapon.ModItem.allowed = allow;
    const old = player.inventory[player.selectedItem]; player.inventory[player.selectedItem] = weapon;
    try {
        player['void ItemCheck_MeleeHitPVP(Item sItem, Rectangle itemRectangle, int damage, float knockBack)'](weapon, target.Hitbox, 10, 0);
        check('attacker callbacks belong to victim process', !modified.length && !notified.length);
        check(allow ? 'allowed attack sends once' : 'veto prevents native send', sent === (allow ? 1 : 0));
    } finally { player.inventory[player.selectedItem] = old; }
}
function verifyVictim(allow) {
    check(allow ? 'victim receives one modified hit' : 'veto prevents remote damage', hurt.length === (allow ? 1 : 0) && (!allow || hurt[0].damage === 7 && hurt[0].from === remote));
    check('victim item callbacks execute exactly once with NetSend state', modified.length === (allow ? 1 : 0) && notified.length === (allow ? 1 : 0)
        && (!allow || modified[0].power === 7 && notified[0].damage === 7 && modified[0].target === Main.myPlayer));
}
export default class PendingNetwork extends Mod {
    Load() { mod = this; this.GetPacket(); }
    HandlePacket(reader, from) {
        const kind = reader.ReadString();
        if (kind === 'hello' && role === 'host') {
            remote = from; helloCount++;
            check('distinct remote player', remote !== Main.myPlayer);
            const count = reader.ReadInt32(), claimable = reader.ReadInt32();
            check('full banner state arrives on join', count === B.killCount[banner()] && claimable === B.claimableBanners[banner()]);
            if (helloCount > 1) { send('rejoined', [], remote); bl.log('pendingnetwork host REJOIN FIM checks=' + checks + ' falhas=' + failures); return; }
            reset(); phase = 'client-veto'; send('client-veto', [Main.myPlayer], remote);
        } else if (kind === 'client-veto' && role === 'client') {
            remote = reader.ReadInt32(); attack(false); send('client-veto-result');
        } else if (kind === 'client-veto-result' && role === 'host') { phase = 'client-veto-check'; due = ticks + 60;
        } else if (kind === 'client-allow' && role === 'client') { attack(true); send('client-allow-result');
        } else if (kind === 'client-allow-result' && role === 'host') { phase = 'client-allow-check'; due = ticks + 60;
        } else if (kind === 'host-prepare' && role === 'client') { reset(); preparePlayers(); send('host-ready');
        } else if (kind === 'host-ready' && role === 'host') { attack(false); send('host-veto-result', [], remote);
        } else if (kind === 'host-veto-result' && role === 'client') { phase = 'host-veto-check'; due = ticks + 60;
        } else if (kind === 'host-allow' && role === 'host') { attack(true); send('host-allow-result', [], remote);
        } else if (kind === 'host-allow-result' && role === 'client') { phase = 'host-allow-check'; due = ticks + 60;
        } else if (kind === 'banner-kill' && role === 'host') {
            B.AddNPCKillBy(ModContent.NPCType(Enemy), Main.myPlayer);
            check('server 50th kill creates a claim', B.killCount[banner()] === 50 && B.claimableBanners[banner()] === 1);
            send('banner-check', [], remote);
        } else if (kind === 'banner-check' && role === 'client') { phase = 'banner-check'; due = ticks + 60;
        } else if (kind === 'banner-done' && role === 'host') {
            check('claim decrement reaches server', B.claimableBanners[banner()] === 0);
            B.AddNPCKillBy(ModContent.NPCType(Enemy), Main.myPlayer);
            send('finished', [], remote);
            bl.log('pendingnetwork host FIM checks=' + checks + ' falhas=' + failures);
        } else if (kind === 'finished' || kind === 'rejoined') {
            bl.log('pendingnetwork client ' + (kind === 'rejoined' ? 'REJOIN ' : '') + 'FIM checks=' + checks + ' falhas=' + failures); phase = 'finished';
        }
    }
}
Main['void DrawInterface(GameTime gameTime)'].hook((original, self, time) => {
    original(self, time); if (Main.gameMenu) return; ticks++;
    if (role === 'host' && !hosting && ticks > 120) {
        hosting = true;
        const Host = bl.classOf('', 'GUIMultiplayerHost'), gui = Host.new(); gui['void .ctor()']();
        gui.maxPlayers = 8; gui._password = ''; gui.Crossplay = false; gui.InviteOnly = false;
        gui['void HostServer()']();
        return;
    }
    if (!started && ticks > 120) {
        if (role === 'host' && (Main.netMode !== 3 || Main.serverBootupInProgress)) return;
        started = true;
        if (role === 'host') {
            B.killCount[banner()] = 49; B.claimableBanners[banner()] = 0;
            bl.log('pendingnetwork HOST READY mode=' + Main.netMode + ' banner=' + banner());
        } else { send('hello', [B.killCount[banner()], B.claimableBanners[banner()]]); bl.log('pendingnetwork CLIENT READY local=' + Main.myPlayer); }
    }
    if (remote >= 0 && phase !== 'finished') preparePlayers();
    if (!due || ticks < due) return;
    due = 0;
    if (phase === 'client-veto-check') { verifyVictim(false); reset(); send('client-allow', [], remote);
    } else if (phase === 'client-allow-check') { verifyVictim(true); send('host-prepare', [], remote);
    } else if (phase === 'host-veto-check') { verifyVictim(false); reset(); send('host-allow');
    } else if (phase === 'host-allow-check') { verifyVictim(true); send('banner-kill');
    } else if (phase === 'banner-check') {
        check('kill and claim update reach client', B.killCount[banner()] === 50 && B.claimableBanners[banner()] === 1);
        claimedBefore = totalItems(); B.RequestBannerClaim(banner(), 1); phase = 'claim-check'; due = ticks + 120;
    } else if (phase === 'claim-check') {
        check('client receives banner item once', totalItems() === claimedBefore + 1);
        check('claim count reaches zero', B.claimableBanners[banner()] === 0);
        B.RequestBannerClaim(banner(), 1); phase = 'duplicate-check'; due = ticks + 120;
    } else if (phase === 'duplicate-check') {
        check('duplicate claim does not duplicate item or underflow', totalItems() === claimedBefore + 1 && B.claimableBanners[banner()] === 0);
        send('banner-done'); phase = 'finished';
    }
});
