#pragma once

#include <charconv>
#include <istream>
#include <ostream>
#include <string>
#include <vector>

namespace bl::runtime::item_save {

struct SavedItem {
    std::string container;
    int slot = 0;
    int stack = 1;
    int prefix = 0;
    bool favorited = false;
    std::string key;
    std::string prefixKey;
    std::string data;
};

inline bool integer(const std::string& text, int& value) {
    const auto result = std::from_chars(text.data(), text.data() + text.size(), value);
    return result.ec == std::errc() && result.ptr == text.data() + text.size();
}

inline bool decode(const std::string& line, SavedItem& out) {
    std::vector<std::string> cols;
    size_t start = 0;

    for (size_t tab; (tab = line.find('\t', start)) != std::string::npos; start = tab + 1) {
        cols.push_back(line.substr(start, tab - start));
    }

    cols.push_back(line.substr(start));
    if ((cols.size() != 6 && cols.size() != 7) || cols[0].empty() || cols[0] == "trash" || cols[5].empty()) return false;

    SavedItem value;
    value.container = cols[0];
    if (!integer(cols[1], value.slot) || value.slot < 0 || !integer(cols[2], value.stack) || value.stack <= 0) return false;
    if (cols[3].find('/') != std::string::npos) value.prefixKey = cols[3];
    else if (!integer(cols[3], value.prefix) || value.prefix < 0 || value.prefix > 255) return false;
    if (cols[4] != "0" && cols[4] != "1") return false;

    value.favorited = cols[4] == "1";
    value.key = cols[5];
    if (cols.size() == 7) value.data = cols[6];

    out = std::move(value);

    return true;
}

inline bool read(std::istream& stream, std::vector<SavedItem>& out) {
    std::string line;
    if (!std::getline(stream, line)) return false;
    if (!line.empty() && line.back() == '\r') line.pop_back();
    if (line != "bunnyloader itens 1" && line != "bunnyloader itens 2") return false;

    std::vector<SavedItem> records;

    while (std::getline(stream, line)) {
        if (!line.empty() && line.back() == '\r') line.pop_back();
        SavedItem value;
        if (decode(line, value)) records.push_back(std::move(value));
    }

    if (stream.bad()) return false;

    out = std::move(records);

    return true;
}

inline bool write(std::ostream& stream, const std::vector<SavedItem>& items) {
    stream << "bunnyloader itens 2\n";

    for (const auto& item : items) {
        // JSON escapes tab/newline/NUL. Reject unescaped delimiters rather than
        // allowing a callback or a damaged file to inject another record.
        for (const auto* text : {&item.container, &item.key, &item.prefixKey, &item.data}) {
            if (text->find_first_of("\t\r\n") != std::string::npos || text->find('\0') != std::string::npos) return false;
        }

        stream << item.container << '\t' << item.slot << '\t' << item.stack << '\t'
               << (item.prefixKey.empty() ? std::to_string(item.prefix) : item.prefixKey) << '\t'
               << (item.favorited ? 1 : 0) << '\t' << item.key << '\t' << item.data << '\n';
    }

    return static_cast<bool>(stream);
}

} // namespace bl::runtime::item_save
