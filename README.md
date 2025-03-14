![A blue waveform logo on a dark background. It consists of angular blue lines forming several peaks and valleys that create a zigzag pattern similar to an audio waveform or electrical signal.](/wiki/assets/img/PAM-banner-wide.png)

A small program for editing PAYDAY 3's audio files. \
<br />
[![Ko-fi Badge](https://img.shields.io/badge/Ko--fi-F16061?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/snoozeds) [![Patreon](https://img.shields.io/badge/Patreon-F96854?style=for-the-badge&logo=patreon&logoColor=white)](https://patreon.com/snoozeds)

Converts the audio file to WAV, 16 bit signed little-endian using [ffmpeg](https://ffmpeg.org/), then converts that WAV file to WEM using [wwise_pd3](https://github.com/MoolahModding/wwise_pd3), and then finally replaces the ubulk file with that WEM, and edits the uexp file to have the size value that's inside the json file (fixes corrupted, glitchy audio).
The program will prompt you where you want to save converted files.

Also has the option to pack files for you using repak if you wish.

-----

### Dependencies:
- This tool requires `ffmpeg` to be installed on your system and added to `PATH`. 
You may install ffmpeg [here](https://ffmpeg.org/download.html).
You can check it is installed correctly by opening command prompt and typing `ffmpeg`

- This tool requires `.json`, `.uasset`, `.uexp`, `.ubulk` files + a `(.wav/.mp3/.ogg/.flac/.aiff/.wma/.m4a/.aac/.opus)` file for conversion.

- This tool requires [repak.exe](https://github.com/trumank/repak/releases/latest/download/repak_cli-x86_64-pc-windows-msvc.zip) if you want to use the Pack Files tab.

-----

### Usage Instructions:
You may find instructions on how to use the app by clicking on the "Help" button. Info inside this dialog will change depending what tab (single file, batch conversion) you are in.

-----

### Previews:
| Modding a single sound | Modding multiple sounds | Searching sound IDs |
| -------- | -------- | -------- |
|[![Modding a single sound](https://img.youtube.com/vi/wbDB-RdiKRY/maxresdefault.jpg)](https://www.youtube.com/watch?v=wbDB-RdiKRY)  | [![Modding multiple sounds](https://img.youtube.com/vi/36ryInl7q3M/maxresdefault.jpg)](https://www.youtube.com/watch?v=36ryInl7q3M) | [![Searching sound IDs](https://img.youtube.com/vi/Y48aZMiaFXo/maxresdefault.jpg)](https://youtu.be/Y48aZMiaFXo)

| Single File Mode | Batch Conversion Mode |
| ---------------- | --------------------- |
| [![Single File Mode](https://storage.modworkshop.net/mods/images/FYzn1R17R2a1XoOP7PpyfGi90TwxEL8Y0NUKUK7M.webp)](https://storage.modworkshop.net/mods/images/FYzn1R17R2a1XoOP7PpyfGi90TwxEL8Y0NUKUK7M.webp) | [![Batch Conversion Mode](https://storage.modworkshop.net/mods/images/qkdGccNtI8GBDHQl464tOywBgcipxxqMPKRW9r6t.webp)](https://storage.modworkshop.net/mods/images/qkdGccNtI8GBDHQl464tOywBgcipxxqMPKRW9r6t.webp) |

| Pack Files | Settings Window |
| ---------- | --------------- |
| [![Pack Files](https://storage.modworkshop.net/mods/images/abGhtMICsljNk4KZDzH9Yzft2bl7zWSCQGlenCOU.webp)](https://storage.modworkshop.net/mods/images/abGhtMICsljNk4KZDzH9Yzft2bl7zWSCQGlenCOU.webp) | [![Settings Window](https://storage.modworkshop.net/mods/images/SgLqorP4g3aWUk3FB3ShGDNW632Zx9Wl44pZIjyO.webp)](https://storage.modworkshop.net/mods/images/SgLqorP4g3aWUk3FB3ShGDNW632Zx9Wl44pZIjyO.webp) |

