#pragma once
#include <string>

namespace bl::runtime::content {

int loadSound(const std::string& path);

int enqueueSound(int group, int id, float left, float right, float rate, int max, bool ignore);
void cancelSound(int handle);
int playbackState(int handle);
int activeSound(int group);

int registerMusic(const std::string& path);

void setMusicVolume(int id, float volume);

int musicState(int id);

}
