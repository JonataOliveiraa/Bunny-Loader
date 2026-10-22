"""English Discord embeds for confirmed catalog changes. Configuration stays local."""
from __future__ import annotations

import datetime
import json
import math
import os
import re
import time
import unicodedata
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG = ROOT / "bunnyloaderdiscordwebhook.txt"
STATE = ROOT / "build" / "discord-updates.json"
REPOSITORY = "https://github.com/JonataOliveiraa/Bunny-Loader-Mods"
RAW = "https://raw.githubusercontent.com/JonataOliveiraa/Bunny-Loader-Mods"
MAX_PER_FLUSH = 10


class NotificationError(Exception):
    pass


def webhook_url() -> str:
    value = os.environ.get("BL_DISCORD_WEBHOOK_URL", "").strip()
    if not value and CONFIG.is_file():
        value = CONFIG.read_text(encoding="utf-8-sig").strip()
    if not value:
        return ""
    try:
        parsed = urllib.parse.urlsplit(value)
    except ValueError:
        raise NotificationError("Invalid Discord webhook configuration.") from None
    if (parsed.scheme != "https" or parsed.netloc != "discord.com" or parsed.fragment
            or not re.fullmatch(r"/api/(?:v\d+/)?webhooks/\d+/[A-Za-z0-9._-]+", parsed.path)):
        raise NotificationError("Invalid Discord webhook configuration.")
    query = dict(urllib.parse.parse_qsl(parsed.query))
    query["wait"] = "true"
    return urllib.parse.urlunsplit(parsed._replace(query=urllib.parse.urlencode(query)))


def text(value: object, limit: int = 512) -> str:
    value = re.sub(r"<a?:\w+:\d+>|[0-9#*]\ufe0f?\u20e3", "", str(value or ""))
    value = "".join(c for c in value if unicodedata.category(c) not in {"So", "Cc", "Cf"}
                    and not 0x1F000 <= ord(c) <= 0x1FFFF
                    and c not in "\ufe0e\ufe0f\u20e3")
    value = re.sub(r"([\\`*_~|\[\]])", r"\\\1", value.strip())
    return value if len(value) <= limit else value[:limit - 3] + "..."


def author_line(manifest: dict) -> str:
    authors = manifest.get("authors") or manifest.get("author") or []
    if isinstance(authors, str):
        authors = [authors]
    return ", ".join(a if isinstance(a, str) else str(a.get("name", ""))
                     for a in authors if isinstance(a, (str, dict)))


def package_type(manifest: dict) -> str:
    kind = str(manifest.get("type", "mod")).strip().lower()
    if kind in {"font", "fonts", "fonte", "fontes"}:
        return "Font pack"
    if kind in {"texture", "textures", "textura", "texturas", "resourcepack", "resource-pack"}:
        return "Texture pack"
    return "Mod"


def payload(kind: str, entry: dict, previous: dict | None, commit: str,
            previous_commit: str, changed_files: list[str]) -> dict:
    title, description, color = {
        "published": ("New package published", "A new package is now available in the Bunny Loader catalog.", 0x3BA55D),
        "updated": ("Package updated", "A new package version is now available in the Bunny Loader catalog.", 0x5865F2),
        "listing": ("Catalog listing updated", "The package's catalog information has been updated.", 0xE8B34B),
        "removed": ("Package removed", "This package has been removed from the Bunny Loader catalog. Its existing GitHub releases remain available.", 0xED4245),
    }[kind]
    manifest = entry.get("manifest") or {}
    version = text(entry.get("version"), 100) or "Not specified"
    fields = [{"name": "Version", "value": version, "inline": True}]
    if previous and previous.get("version") != entry.get("version"):
        fields.append({"name": "Previous version", "value": text(previous.get("version"), 100) or "Not specified", "inline": True})
    fields.extend([
        {"name": "Package type", "value": package_type(manifest), "inline": True},
        {"name": "Authors", "value": text(author_line(manifest)) or "Not specified", "inline": False},
    ])
    size = entry.get("download", {}).get("size")
    if isinstance(size, (int, float)) and size >= 0:
        unit, amount = ("MB", size / 1048576) if size >= 1048576 else ("KB", size / 1024)
        fields.append({"name": "Package size", "value": f"{amount:.1f} {unit}", "inline": True})
    fields.append({"name": "Package ID", "value": text(entry.get("id"), 100) or "Not specified", "inline": True})
    release = f"{REPOSITORY}/releases/tag/mod-{entry['id']}-v{entry['version']}"
    links = [f"[{'Previous release' if kind == 'removed' else 'View release'}]({release})"]
    if kind != "removed":
        links.append(f"[Download package]({entry['download']['url']})")
    fields.append({"name": "Links", "value": "\n".join(links)[:1024], "inline": False})
    if kind == "listing":
        changes = []
        if previous and previous.get("manifest") != entry.get("manifest"):
            changes.append("Package details")
        if any(f.startswith(("description", "changelog", "license")) for f in changed_files):
            changes.append("Description, release notes or license")
        if any(f.startswith(("icon.", "banner.", "authors/", "thumbnails/")) for f in changed_files):
            changes.append("Images or author profiles")
        if changes:
            fields.append({"name": "Changes", "value": "; ".join(changes), "inline": False})
    embed = {
        "title": text(title + ": " + str(entry.get("name") or entry["id"]), 256),
        "description": description,
        "url": release,
        "color": color,
        "fields": fields,
        "timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "footer": {"text": "Bunny Loader Catalog"},
    }
    icon = next((f for f in ("icon.gif", "icon.png") if f in entry.get("files", [])), None)
    if icon:
        revision = previous_commit if kind == "removed" else commit
        embed["thumbnail"] = {"url": f"{RAW}/{revision}/mods/{entry['uid']}/{icon}"}
    return {"username": "Bunny Loader Updates", "allowed_mentions": {"parse": []}, "embeds": [embed]}


def catalog_events(before: dict, after: dict, previous_commit: str, commit: str,
                   paths: list[str]) -> list[dict]:
    old = {m["uid"]: m for m in before.get("mods", [])}
    new = {m["uid"]: m for m in after.get("mods", [])}
    changed = {}
    for path in paths:
        parts = path.split("/", 2)
        if len(parts) == 3 and parts[0] == "mods":
            changed.setdefault(parts[1], []).append(parts[2])
    events = []
    for uid in sorted(set(old) | set(new)):
        previous, entry = old.get(uid), new.get(uid)
        if previous == entry and uid not in changed:
            continue
        if entry is None:
            kind, entry = "removed", previous
        elif previous is None:
            kind = "published"
        elif previous.get("version") != entry.get("version"):
            kind = "updated"
        else:
            kind = "listing"
        events.append({"id": f"{commit}:{uid}", "payload": payload(
            kind, entry, previous, commit, previous_commit, changed.get(uid, []))})
    return events


def load_state() -> dict:
    if not STATE.is_file():
        return {"pending": [], "delivered": [], "last_error": "", "retry_at": 0}
    try:
        state = json.loads(STATE.read_text(encoding="utf-8"))
        if (not isinstance(state, dict) or not isinstance(state.get("pending"), list)
                or not isinstance(state.get("delivered"), list)
                or any(not isinstance(event, dict) or not isinstance(event.get("id"), str)
                       or not isinstance(event.get("payload"), dict) for event in state["pending"])
                or any(not isinstance(event, str) for event in state["delivered"])
                or not isinstance(state.get("retry_at", 0), (int, float))
                or not math.isfinite(state.get("retry_at", 0))):
            raise ValueError
        return state
    except (ValueError, TypeError):
        raise NotificationError("Discord notification history is unreadable; pending notifications were preserved.") from None


def save_state(state: dict) -> None:
    STATE.parent.mkdir(parents=True, exist_ok=True)
    temporary = STATE.with_suffix(".tmp")
    temporary.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(STATE)


def status() -> dict:
    try:
        configured = bool(webhook_url())
        state = load_state()
        return {"configured": configured, "pending": len(state["pending"]),
                "error": state.get("last_error", ""), "retry_at": state.get("retry_at", 0)}
    except (NotificationError, OSError):
        return {"configured": False, "pending": 0, "error": "Check the local Discord configuration and notification history."}


def send(url: str, body: dict) -> dict:
    request = urllib.request.Request(url, data=json.dumps(body).encode("utf-8"), method="POST", headers={
        "Content-Type": "application/json", "User-Agent": "BunnyLoader-Catalog/1.0",
    })
    with urllib.request.urlopen(request, timeout=10) as response:
        message = json.load(response)
    if not isinstance(message, dict) or not message.get("id"):
        raise NotificationError("Discord did not confirm the notification.")
    return message


def flush() -> None:
    try:
        url = webhook_url()
        if not url:
            return
        state = load_state()
        if state.get("retry_at", 0) > time.time():
            print("  Discord: waiting for the rate limit before retrying pending notifications.")
            return
        for _ in range(min(len(state["pending"]), MAX_PER_FLUSH)):
            event = state["pending"][0]
            try:
                send(url, event["payload"])
            except urllib.error.HTTPError as error:
                retry = 0
                if error.code == 429:
                    try:
                        retry = max(1, float(json.loads(error.read()).get("retry_after", 1)))
                    except (ValueError, TypeError, AttributeError):
                        retry = 60
                state["retry_at"] = time.time() + retry
                state["last_error"] = f"Discord returned HTTP {error.code}. Pending notifications will be retried."
                save_state(state)
                print("  " + state["last_error"])
                return
            except (OSError, ValueError, NotificationError):
                state["last_error"] = "Discord could not confirm delivery. Pending notifications will be retried."
                save_state(state)
                print("  " + state["last_error"])
                return
            state["pending"].pop(0)
            state["delivered"] = (state["delivered"] + [event["id"]])[-2000:]
            state["last_error"], state["retry_at"] = "", 0
            save_state(state)
            print("  Discord notification sent.")
        if state["pending"]:
            print(f"  Discord: {len(state['pending'])} notification(s) pending.")
    except (NotificationError, OSError, ValueError, TypeError):
        print("  Discord notifications could not be processed. Check the local configuration and notification history.")


def notify(events: list[dict]) -> None:
    try:
        if not webhook_url():
            return
        state = load_state()
        known = set(state["delivered"]) | {e["id"] for e in state["pending"]}
        for event in events:
            if event["id"] not in known:
                state["pending"].append(event)
                known.add(event["id"])
        save_state(state)
    except (NotificationError, OSError, ValueError, TypeError):
        print("  Discord notifications could not be queued. The catalog operation succeeded.")
        return
    flush()


def send_test() -> str:
    url = webhook_url()
    if not url:
        raise NotificationError("Discord webhook is not configured.")
    body = {"username": "Bunny Loader Updates", "allowed_mentions": {"parse": []}, "embeds": [{
        "title": "Catalog notifications connected",
        "description": "Bunny Loader will post confirmed package publications, version updates, catalog listing changes and removals in this channel.",
        "color": 0x5865F2, "footer": {"text": "Bunny Loader Catalog | Connection test"},
        "timestamp": datetime.datetime.now(datetime.timezone.utc).isoformat(),
    }]}
    try:
        message = send(url, body)
    except urllib.error.HTTPError as error:
        raise NotificationError(f"Discord returned HTTP {error.code}; check the webhook configuration.") from None
    except (OSError, ValueError):
        raise NotificationError("Discord could not be reached.") from None
    print("Discord connection test sent.")
    return str(message["id"])
