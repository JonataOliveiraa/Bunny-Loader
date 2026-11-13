#pragma once

namespace bl::runtime {

void requestRestart(bool save);
bool restartReady();
void tickRestart();

void installDevTools();
void setGameFrozen(bool on);

}
