![](/wiki/assets/img/PAM-banner-wide.png)

## PD3AudioModder
A small program for editing PAYDAY 3 (and other non-iostore Unreal Engine games)'s audio files.

Converts the audio file to WAV, 16 bit signed little-endian using [ffmpeg](https://ffmpeg.org/), then converts that WAV file to WEM using [wwise_pd3](https://github.com/MoolahModding/wwise_pd3), and then finally replaces the ubulk file with that WEM, and edits the uexp file to have the size value that's inside the json file (fixes corrupted, glitchy audio).
The program will prompt you where you want to save converted files.


-----

### Dependencies:
- This tool requires `ffmpeg` to be installed on your system and added to `PATH`. 
You may install ffmpeg [here](https://ffmpeg.org/download.html).
You can check it is installed correctly by opening command prompt and typing `ffmpeg`

- This tool reuires `.json`, `.uasset`, `.uexp`, `.ubulk` files + a `(.wav/.mp3/.ogg/.flac/.aiff/.wma/.m4a/.aac/.opus)` file for conversion.

-----

### Usage Instructions:
You may find instructions on how to use the app by clicking on the "Help" button. Info inside this dialog will change depending what tab (single file, batch conversion) you are in.

-----

### Previews:
| Modding a single sound | Modding multiple sounds |
| -------- | -------- |
|[![Modding a single sound](https://img.youtube.com/vi/wbDB-RdiKRY/maxresdefault.jpg)](https://www.youtube.com/watch?v=wbDB-RdiKRY)  | [![Modding multiple sounds](https://img.youtube.com/vi/36ryInl7q3M/maxresdefault.jpg)](https://www.youtube.com/watch?v=36ryInl7q3M)
