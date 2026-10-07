// Instrumentação opcional do fixture. Nenhuma alteração no áudio de produção.
let playMs = 0;
let stopMs = 0;
let plays = 0;
let stops = 0;
let failed = 0;
let transport = 'play';

export function installProfile() {
    const playKey = typeof bl.sounds.enqueue === 'function' ? 'enqueue' : 'play';
    transport = playKey;
    const stopKey = playKey === 'enqueue' ? 'cancel' : 'stop';
    const originalPlay = bl.sounds[playKey];
    const originalStop = bl.sounds[stopKey];

    bl.sounds[playKey] = (...args) => {
        const started = performance.now();
        const stream = originalPlay(...args);
        playMs += performance.now() - started;
        plays++;
        if (stream <= 0) failed++;
        return stream;
    };

    bl.sounds[stopKey] = stream => {
        const started = performance.now();
        originalStop(stream);
        stopMs += performance.now() - started;
        stops++;
    };
}

export function resetProfile() {
    playMs = 0;
    stopMs = 0;
    plays = 0;
    stops = 0;
    failed = 0;
}

export function profileResult() {
    return { transport, playMs, stopMs, plays, stops, failed };
}
