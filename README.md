# SC4LTEXTT : SC4 LTEXT Translator
![GitHub all releases](https://img.shields.io/github/downloads/noah-severyn/SC4LTEXTT/total?style=flat-square)

An assistant for translating Simcity 4 [LTEXT](https://wiki.sc4devotion.com/index.php?title=LTEXT) files.

To use, select a DBPF file housing the LTEXT files you wish to translate. Choose the language you wish to translate to, and translate each of the desired LTEXT files, either manually or automatically via the Azure AI Text Translation service. Each LTEXT will turn green to indicate it has been translated. *SC4LTEXTT* can currently translate to all of the 18 languages supported in-game. More languages are possible; let me know if you have one you would like to add. An internet connection is required for the automatic translations.

Each LTEXT GID will be incremented by the appropriate offset for the translated language. Currently all LTEXT files are written uncompressed.

It is important to note that due to [reported issues](https://community.simtropolis.com/forums/topic/761192-please-help-mz-crime-and-police-language-translations/?do=findComment&comment=1762752) with how Reader handles text encoding, certain 4-byte characters will appear as garbage data when viewed in Reader. This applies to all characters of languages such as Chinese, Korean, Thai, and Japanese, but also certain diacriticals for European languages too. If you open a file containing LTEXTs with these characters in Reader, **DO NOT SAVE**. If you do, the correctly functioning (but merely incorrectly displayed) characters will be overwritten by Reader with the garbage displayed characters.

![](/img/ProductImage.png)


## System Requirements
- .NET 8
- Windows 10, version 1607 or newer
