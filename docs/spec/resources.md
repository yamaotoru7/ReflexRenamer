# オリジナル exe のリソース一覧

`dotnet run --project tools/FrTools -- res-dump` で生成。手で編集しない。座標はダイアログ単位（DLU）。

## ダイアログ

### DIALOG 100 「About Flexible Renamer」

- サイズ 235x49、スタイル 0x80C800C0、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| -1 | Static | #64 | 10,9,20,20 | 0x50000003 |
| 1025 | Static | IDC_APP_VERSION | 40,9,134,9 | 0x50020080 |
| 1027 | Static | IDC_APP_COPYRIGHT | 40,22,98,9 | 0x50020000 |
| 1 | Button(Default) | OK | 178,5,50,14 | 0x50030001 |
| 1028 | Static | IDC_APP_MAIL | 140,22,88,9 | 0x50020000 |
| 1043 | Static | IDC_WEB_SITE | 40,34,188,9 | 0x50020000 |

### DIALOG 130 「」

- サイズ 259x66、スタイル 0x80C000C0、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| -1 | Static | #140 | 9,15,20,20 | 0x50000003 |
| 1032 | Edit |  | 35,6,217,40 | 0x50001884 |
| 6 | Button(Default) | &Retry | 26,46,50,14 | 0x50010001 |
| 4 | Button | Skip | 82,46,50,14 | 0x50010000 |
| 5 | Button | Skip All | 134,46,50,14 | 0x50010000 |
| 2 | Button | Cancel | 189,46,50,14 | 0x50010000 |

### DIALOG 131 「」

- サイズ 100x76、スタイル 0x50000040、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 1000 | SysTreeView32 | Tree1 | 0,0,100,76 | 0x50810233 |

### DIALOG 133 「」

- サイズ 187x67、スタイル 0x90C000C0、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 2 | Button(Default) | Cancel | 67,48,50,14 | 0x50010001 |
| 1009 | msctls_progress32 |  | 15,32,154,11 | 0x50800001 |
| 1013 | Static |  0 % | 13,20,158,9 | 0x50020001 |
| 1016 | Static |  | 13,6,158,9 | 0x50020000 |

### DIALOG 139 「Order Preset」

- サイズ 349x150、スタイル 0x90CC0040、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 1088 | SysListView32 |  | 7,6,335,102 | 0x5081820D |
| 1051 | Button | Higher | 7,110,40,14 | 0x50010000 |
| 1052 | Button | Lower | 7,125,40,14 | 0x50010000 |
| 1054 | Button | Modify Name | 211,111,64,14 | 0x50010000 |
| 1053 | Button | Delete | 278,111,64,14 | 0x50010000 |
| 1 | Button | Save && Close | 211,130,64,14 | 0x50010000 |
| 2 | Button | Cancel | 278,130,64,14 | 0x50010000 |

### DIALOG 141 「」

- サイズ 137x258、スタイル 0x40800040、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 1009 | Static |  | 5,7,20,20 | 0x50000003 |
| 1151 | Static | \nModify attributes/timestamps of file/folder. | 29,5,108,26 | 0x50020080 |
| 1174 | Static |  | 7,31,129,1 | 0x50000010 |
| -1 | Static | Target: | 7,35,30,9 | 0x5002008C |
| 1033 | CheckBox | &File | 40,37,96,9 | 0x50010003 |
| 1034 | CheckBox | Fol&der | 40,46,96,9 | 0x50010003 |
| 1024 | CheckBox | Recursive Su&b Folders | 7,55,130,9 | 0x50010003 |
| -1 | Static |  | 7,67,129,1 | 0x50000010 |
| -1 | Static | Timestamp: | 7,71,45,9 | 0x50020000 |
| 1017 | ComboBox |  | 55,70,72,73 | 0x50210003 |
| 1118 | Static | Date &Created: | 7,83,130,9 | 0x5002000C |
| 1040 | SysDateTimePick32 |  | 7,92,120,13 | 0x50010022 |
| 1119 | Static | Date &Updated: | 7,106,130,9 | 0x5002000C |
| 1042 | SysDateTimePick32 |  | 7,114,120,13 | 0x50010022 |
| 1120 | Static | Date &Accessed: | 7,128,130,9 | 0x5002000C |
| 1043 | SysDateTimePick32 |  | 7,136,120,13 | 0x50010022 |
| 1143 | RadioButton | to &Earlier | 7,87,55,10 | 0x50030009 |
| 1144 | RadioButton | to &Later | 67,87,55,10 | 0x50000009 |
| 1126 | Static | Day: | 7,105,16,9 | 0x50020000 |
| 1125 | Edit |  | 23,102,35,12 | 0x50812082 |
| 1133 | msctls_updown32 |  | 127,102,10,10 | 0x500000B6 |
| 1127 | Static | Time: | 62,105,18,8 | 0x50020000 |
| 1047 | SysDateTimePick32 |  | 84,102,43,12 | 0x40010029 |
| 1128 | Static | Min: | 7,88,30,9 | 0x5002008C |
| 1045 | SysDateTimePick32 |  | 37,85,90,13 | 0x40010020 |
| 1129 | Static | Max: | 7,104,30,9 | 0x5002008C |
| 1046 | SysDateTimePick32 |  | 37,102,90,13 | 0x40010020 |
| 1124 | Static | Base Date: | 7,88,45,10 | 0x5002008C |
| 1139 | RadioButton | Date &Created | 57,88,80,10 | 0x50030009 |
| 1140 | RadioButton | Date &Updated | 57,97,80,10 | 0x50000009 |
| 1141 | RadioButton | Date &Accessed | 57,106,80,10 | 0x50000009 |
| 1142 | RadioButton | Date Photo &Taken | 57,115,80,10 | 0x50000009 |
| 1130 | Static | Apply to: | 7,126,45,9 | 0x5002008C |
| 1121 | CheckBox | Date &Created | 57,126,80,10 | 0x40010003 |
| 1122 | CheckBox | Date &Updated | 57,136,80,10 | 0x40010003 |
| 1123 | CheckBox | Date &Accessed | 57,145,80,10 | 0x40010003 |
| 1025 | CheckBox | Modify Folder Timestamp | 7,156,129,15 | 0x58012003 |
| -1 | Static |  | 7,173,129,1 | 0x50000010 |
| -1 | Static | Attribute: | 7,178,45,9 | 0x5002008C |
| 1004 | CheckBox(3state) | &Read Only | 57,176,80,9 | 0x50010006 |
| 1005 | CheckBox(3state) | Archi&ve | 57,185,80,9 | 0x50010006 |
| 1008 | CheckBox(3state) | &Hidden | 57,194,80,9 | 0x50010006 |
| 1012 | CheckBox(3state) | S&ystem | 57,203,80,9 | 0x50010006 |
| -1 | Static | Keep its Attributes,\n with the Grayed Items | 0,214,137,18 | 0x50020001 |
| 1029 | Static |  | 7,236,129,1 | 0x50000010 |
| 1011 | Button | &Start Modify | 72,240,54,14 | 0x50010000 |
| 1000 | SysListView32 |  | 0,0,119,9 | 0x50010009 |
| 1001 | SysListView32 |  | 0,0,119,9 | 0x50010009 |

### DIALOG 142 「」

- サイズ 137x248、スタイル 0x40800040、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 1009 | Static |  | 5,7,20,20 | 0x50000003 |
| 1151 | Static | Create numbered object\n(Sized File/Folder),\nunder the selected folder. | 29,5,108,26 | 0x50020080 |
| 1174 | Static |  | 7,31,129,1 | 0x50000010 |
| -1 | Static | Kind of Target Object: | 7,35,130,9 | 0x5002008C |
| 1077 | RadioButton | Empty &File (FileSize Available) | 15,48,121,10 | 0x50030009 |
| 1078 | RadioButton | Copied File | 15,72,121,10 | 0x50010009 |
| 1079 | RadioButton | Empty Fol&der | 15,99,121,10 | 0x50010009 |
| 1080 | RadioButton | Copied Folder | 15,111,121,10 | 0x50010009 |
| 1002 | Edit |  | 32,58,23,13 | 0x50810082 |
| 1054 | Static | Byte(K/M/G Available) | 58,60,79,10 | 0x5002008C |
| 1058 | Edit |  | 32,82,81,13 | 0x50810080 |
| 1073 | Button | ... | 114,82,15,14 | 0x50010000 |
| 1059 | Edit |  | 32,122,81,13 | 0x50810080 |
| 1074 | Button | ... | 114,121,15,14 | 0x50010000 |
| -1 | Static |  | 7,138,129,1 | 0x50000010 |
| -1 | Static | Name: | 7,143,120,9 | 0x5002008C |
| 1018 | Edit |  | 7,152,120,12 | 0x50810080 |
| -1 | Static | In case of plural objects,\nobjects numbered at the position of '?'\n and digit equals to the number of '?'. | 7,166,129,23 | 0x50000081 |
| -1 | Static | Amount: | 7,192,42,9 | 0x5002008C |
| 1022 | Edit |  | 55,190,48,12 | 0x50812082 |
| -1 | Static | Start from: | 7,205,42,9 | 0x50020000 |
| 1065 | Edit |  | 55,202,48,12 | 0x50810082 |
| -1 | Static | Step: | 7,217,42,9 | 0x50020000 |
| 1067 | Edit |  | 55,214,48,12 | 0x50810082 |
| 1029 | Static |  | 7,227,129,1 | 0x50000010 |
| 1011 | Button | &Start Create | 72,231,54,14 | 0x50010000 |
| 1000 | SysListView32 |  | 0,0,119,9 | 0x50019009 |

### DIALOG 143 「」

- サイズ 137x268、スタイル 0x50800040、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 1009 | Static |  | 5,7,20,20 | 0x50000003 |
| 1151 | Static | \nRename file/folder. | 28,5,108,26 | 0x50020080 |
| -1 | Static | Target: | 7,35,30,9 | 0x5002008C |
| 1033 | CheckBox | &File | 40,37,96,9 | 0x50010003 |
| 1034 | CheckBox | Fol&der | 40,46,96,9 | 0x50010003 |
| -1 | Static | Filter: | 7,58,23,9 | 0x5002008C |
| 1057 | ComboBox |  | 34,57,89,226 | 0x50210042 |
| 1024 | CheckBox | Recursive Su&b Folders | 7,72,83,9 | 0x50010003 |
| 1053 | Static | Depth | 93,72,21,9 | 0x58020000 |
| 1052 | Edit |  | 116,71,19,12 | 0x58812082 |
| -1 | Static |  | 7,83,129,1 | 0x50000010 |
| 1025 | CheckBox | &Advanced Rename | 7,88,82,9 | 0x50010003 |
| 1048 | Button | Preset | 86,85,40,14 | 0x40010000 |
| 1016 | Static |  | 0,99,137,132 | 0x40000007 |
| 1035 | Static | Rename Method: | 7,100,74,9 | 0x4002008C |
| 1017 | ComboBox |  | 121,0,16,72 | 0x40210003 |
| 1037 | RadioButton | &Wild Card | 71,100,63,10 | 0x40030009 |
| 1038 | RadioButton | Reg &Exp | 71,109,50,10 | 0x40010009 |
| 1039 | RadioButton | &Translation | 71,118,50,10 | 0x40010009 |
| 1051 | Button |  | 121,110,13,12 | 0x48010000 |
| 1036 | Static | Search: | 7,133,30,9 | 0x4002008C |
| 1045 | Button | Support | 86,129,40,14 | 0x40010000 |
| 1010 | ComboBox |  | 7,143,120,169 | 0x40210042 |
| 1040 | Static | Replace: | 7,160,30,9 | 0x4002008C |
| 1046 | Button | Support | 86,156,40,14 | 0x40010000 |
| 1019 | ComboBox |  | 7,170,120,169 | 0x40210142 |
| 1055 | Static | IDC_STATIC_ONEPOINT | 7,183,120,26 | 0x40000081 |
| 1050 | Button |  | 113,0,13,12 | 0x40010000 |
| 1000 | SysListView32 |  | 0,0,117,9 | 0x50010009 |
| 1001 | SysListView32 |  | 0,0,117,9 | 0x50010009 |
| 1026 | CheckBox |  | 38,160,8,9 | 0x40010003 |
| 1047 | Button | Script | 46,156,40,14 | 0x48010000 |
| 1029 | Static |  | 7,231,128,1 | 0x50000010 |
| 1011 | Button(OwnerDraw) | &Rename | 72,234,54,14 | 0x5001000B |
| 1049 | Button | Option | 7,250,60,14 | 0x50010000 |
| 1023 | Button | &Undo | 72,250,54,14 | 0x50010000 |
| 1146 | ComboBox |  | 7,234,64,226 | 0x50210043 |
| 1081 | Static | Start: | 8,221,20,9 | 0x40020002 |
| 1065 | Edit |  | 31,218,32,12 | 0x40812082 |
| 1140 | msctls_updown32 |  | 57,218,11,13 | 0x400000B6 |
| 1054 | Button |  | 124,208,13,12 | 0x40010000 |
| 1084 | Static | Numbering: | 6,209,35,9 | 0x4002000C |
| 1083 | Static | Step: | 67,221,28,9 | 0x40020002 |
| 1067 | Edit |  | 97,218,24,12 | 0x40812082 |
| 1142 | msctls_updown32 |  | 115,218,10,13 | 0x400000B6 |
| 1174 | Static |  | 7,31,129,1 | 0x50000010 |
| 1173 | CheckBox | Reset by each folder | 47,209,76,9 | 0x40010003 |

### DIALOG 145 「」

- サイズ 140x268、スタイル 0x40000040、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|

### DIALOG 146 「」

- サイズ 139x118、スタイル 0x50000440、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 1015 | Static | Menu | 2,0,23,9 | 0x50020000 |
| 1014 | SysTreeView32 |  | 0,9,131,84 | 0x50811037 |
| 1081 | Static | StartNum: | 2,96,33,9 | 0x4002000C |
| 1065 | Edit |  | 36,94,32,12 | 0x40810082 |
| 1140 | msctls_updown32 |  | 62,94,11,13 | 0x400000B6 |
| 1030 | Static | Position: | 2,96,28,9 | 0x4002000C |
| 1031 | Edit |  | 36,94,32,12 | 0x40812082 |
| 1138 | msctls_updown32 |  | 62,94,11,13 | 0x400000B6 |
| 1023 | Static | Digit: | 77,96,19,9 | 0x4002000C |
| 1019 | Edit |  | 97,94,26,12 | 0x40812082 |
| 1136 | msctls_updown32 |  | 117,94,10,13 | 0x400000B6 |
| 1082 | Static | Value: | 70,96,33,9 | 0x4002000C |
| 1066 | Edit |  | 104,94,32,12 | 0x40810082 |
| 1141 | msctls_updown32 |  | 130,94,11,13 | 0x400000B6 |
| 1021 | Static | AddString: | 2,108,40,9 | 0x4002000C |
| 1018 | ComboBox |  | 44,106,80,80 | 0x40210042 |
| 1028 | Static | Sample: | 2,96,30,9 | 0x40020000 |
| 1044 | Static |  | 34,94,90,12 | 0x4002000C |
| 1022 | Static | TimeFormat: | 2,108,40,9 | 0x40020000 |
| 1045 | ComboBox |  | 44,106,80,80 | 0x40210042 |
| 1024 | Static | SrcString: | 2,97,40,9 | 0x4002000C |
| 1025 | ComboBox |  | 44,94,80,80 | 0x40210042 |
| 1026 | Static | DestString: | 2,108,40,9 | 0x40020000 |
| 1027 | ComboBox |  | 44,106,80,80 | 0x40210042 |
| 1032 | Static | Advanced Rename (selected item) | 26,0,112,9 | 0x50020102 |

### DIALOG 166 「Numbering」

- サイズ 131x103、スタイル 0x80C800C0、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| -1 | Static | Start from: | 7,9,42,9 | 0x50020000 |
| 1065 | Edit |  | 55,6,40,12 | 0x50810082 |
| 1140 | msctls_updown32 |  | 95,6,11,13 | 0x500000B6 |
| -1 | Static | Step: | 7,21,42,9 | 0x50020000 |
| 1067 | Edit |  | 55,19,40,12 | 0x50810082 |
| 1142 | msctls_updown32 |  | 95,19,11,13 | 0x500000B6 |
| 1 | Button(Default) | OK | 17,83,50,14 | 0x50010001 |
| 2 | Button | Cancel | 73,83,50,14 | 0x50010000 |
| -1 | GroupBox | Number Type | 7,36,117,43 | 0x50000007 |
| 1155 | RadioButton | Decimal Number | 18,47,92,9 | 0x50020009 |
| 1156 | RadioButton | Hexadecimal Number | 18,57,92,9 | 0x50000009 |
| 1157 | RadioButton | Alphabetical Number | 18,66,92,9 | 0x50000009 |

### DIALOG 168 「Verification of treatment in confliction of file name」

- サイズ 259x162、スタイル 0x80C000C0、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| -1 | Static | #142 | 9,15,20,20 | 0x50000003 |
| 1032 | Edit |  | 35,6,217,41 | 0x50001884 |
| -1 | GroupBox | Treatment | 34,50,206,85 | 0x50000007 |
| -1 | Static |  | 110,58,1,73 | 0x50000011 |
| 1059 | RadioButton | &Retry | 51,65,54,9 | 0x50020009 |
| 1060 | RadioButton | &Skip | 51,74,54,9 | 0x50000009 |
| 1061 | RadioButton | Skip All | 51,84,54,9 | 0x50000009 |
| 1062 | RadioButton | Overwrite | 51,93,54,9 | 0x50000009 |
| 1063 | RadioButton | Overwrite All | 51,103,54,9 | 0x50000009 |
| 1064 | RadioButton | Auto &Numbering | 119,63,105,9 | 0x50000009 |
| 1071 | Static | Format: | 130,76,28,9 | 0x58020000 |
| 1070 | Edit |  | 163,74,40,12 | 0x58810080 |
| 1073 | Static | Ex.: (??) | 206,76,29,9 | 0x58020000 |
| 1072 | Static | Position: | 130,87,28,9 | 0x58020000 |
| 1068 | RadioButton | S&tart | 162,87,56,9 | 0x58020009 |
| 1069 | RadioButton | &End | 162,96,56,9 | 0x58000009 |
| 1081 | Static | Start from: | 130,110,40,9 | 0x58020000 |
| 1065 | Edit |  | 174,107,48,12 | 0x58810082 |
| 1140 | msctls_updown32 |  | 222,106,11,13 | 0x580000B6 |
| 1083 | Static | Step: | 130,122,40,9 | 0x58020000 |
| 1067 | Edit |  | 174,120,48,12 | 0x58810082 |
| 1142 | msctls_updown32 |  | 222,120,11,13 | 0x580000B6 |
| 1 | Button(Default) | Con&tinue | 135,141,50,14 | 0x50010001 |
| 2 | Button | &Cancel | 190,141,50,14 | 0x50010000 |

### DIALOG 170 「」

- サイズ 137x268、スタイル 0x50800040、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 1009 | Static |  | 5,7,20,20 | 0x50000003 |
| 1151 | Static | \nRename file, \nusing tag information. | 28,5,108,26 | 0x50020080 |
| 1174 | Static |  | 7,31,129,1 | 0x50000010 |
| -1 | Static | Target: | 7,35,37,9 | 0x5002008C |
| 1048 | Button | Preset | 86,32,40,14 | 0x50010000 |
| 1055 | Static | IDC_STATIC_ONEPOINT | 11,98,120,27 | 0x50000081 |
| 1170 | Static | FileType: | 7,126,64,10 | 0x5002008C |
| 1160 | SysListView32 |  | 11,137,120,23 | 0x50814001 |
| 1024 | CheckBox | Recursive Su&b Folders | 7,164,83,9 | 0x50010003 |
| 1053 | Static | Depth | 93,164,21,9 | 0x58020000 |
| 1052 | Edit |  | 116,163,19,12 | 0x58812082 |
| 1090 | Static |  | 7,176,129,1 | 0x50000010 |
| 1040 | Static | Format: | 7,184,30,9 | 0x5002008C |
| 1046 | Button | Tag List | 86,180,40,14 | 0x50010000 |
| 1019 | ComboBox |  | 7,195,120,169 | 0x50210142 |
| 1029 | Static |  | 7,231,128,1 | 0x50000010 |
| 1011 | Button(OwnerDraw) | &Rename | 72,234,54,14 | 0x5001000B |
| 1049 | Button | Option | 7,250,60,14 | 0x50010000 |
| 1023 | Button | &Undo | 72,250,54,14 | 0x50010000 |
| 1081 | Static | Start: | 8,221,20,9 | 0x50020002 |
| 1065 | Edit |  | 31,218,32,12 | 0x50812082 |
| 1140 | msctls_updown32 |  | 57,218,11,13 | 0x500000B6 |
| 1054 | Button |  | 124,208,13,12 | 0x50010000 |
| 1084 | Static | Numbering: | 6,209,35,9 | 0x5002000C |
| 1083 | Static | Step: | 67,221,28,9 | 0x50020002 |
| 1067 | Edit |  | 97,218,24,12 | 0x50812082 |
| 1142 | msctls_updown32 |  | 115,218,10,13 | 0x500000B6 |
| 1173 | CheckBox | Reset by each folder | 47,209,76,9 | 0x50010003 |
| 1057 | ComboBox |  | 121,0,16,226 | 0x40210042 |
| 1030 | Static | Edit... | 107,46,30,9 | 0x50020100 |
| 1047 | Button | Script | 46,180,40,14 | 0x50010000 |
| 1026 | CheckBox |  | 38,184,8,9 | 0x50010003 |
| 1169 | CheckBox | Use file &property info. | 11,46,96,9 | 0x50010003 |
| 1088 | ListBox |  | 11,56,120,42 | 0x50A10111 |
| 1146 | ComboBox |  | 7,234,64,226 | 0x50210043 |
| 1001 | SysListView32 |  | 0,0,119,9 | 0x50010009 |
| 1000 | SysListView32 |  | 0,0,119,9 | 0x50010009 |

### DIALOG 172 「TagRename」

- サイズ 214x206、スタイル 0x80C800C0、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| -1 | GroupBox | DateTime Format | 7,6,199,85 | 0x50000007 |
| -1 | Static | The form at the time of taking out time from tag information can be set up.  | 21,18,174,17 | 0x50020000 |
| 1028 | Static | Sample: | 36,37,140,9 | 0x50020000 |
| 1044 | Static |  | 36,46,140,12 | 0x5002000C |
| 1022 | Static | DateTime Format: (Editable) | 36,59,140,9 | 0x50020000 |
| 1045 | ComboBox |  | 36,68,140,80 | 0x50210002 |
| -1 | GroupBox | Invalid Characters | 7,95,199,86 | 0x50000007 |
| -1 | Static | The invalid character contained in tag information is related with another character, and can be done. | 21,107,174,17 | 0x50020000 |
| -1 | Static | \ | 36,127,8,12 | 0x50020201 |
| -1 | Static | -> | 46,130,8,9 | 0x50020000 |
| 1091 | Edit |  | 54,127,22,12 | 0x50810081 |
| -1 | Static | / | 85,127,8,12 | 0x50020201 |
| -1 | Static | -> | 95,129,8,9 | 0x50020000 |
| 1092 | Edit |  | 103,127,22,12 | 0x50810081 |
| -1 | Static | : | 136,127,8,12 | 0x50020201 |
| -1 | Static | -> | 146,129,8,9 | 0x50020000 |
| 1093 | Edit |  | 154,127,22,12 | 0x50810081 |
| -1 | Static | * | 36,142,8,12 | 0x50020201 |
| -1 | Static | -> | 46,145,8,9 | 0x50020000 |
| 1094 | Edit |  | 54,142,22,12 | 0x50810081 |
| -1 | Static | ? | 85,141,8,12 | 0x50020201 |
| -1 | Static | -> | 95,144,8,9 | 0x50020000 |
| 1095 | Edit |  | 103,141,22,12 | 0x50810081 |
| -1 | Static | " | 136,141,8,12 | 0x50020201 |
| -1 | Static | -> | 146,144,8,9 | 0x50020000 |
| 1096 | Edit |  | 154,141,22,12 | 0x50810081 |
| -1 | Static | < | 36,157,8,12 | 0x50020201 |
| -1 | Static | -> | 46,161,8,9 | 0x50020000 |
| 1097 | Edit |  | 54,157,22,12 | 0x50810081 |
| -1 | Static | > | 85,156,8,12 | 0x50020201 |
| -1 | Static | -> | 95,159,8,9 | 0x50020000 |
| 1098 | Edit |  | 103,156,22,12 | 0x50810081 |
| -1 | Static | \| | 136,156,8,12 | 0x50020201 |
| -1 | Static | -> | 146,159,8,9 | 0x50020000 |
| 1099 | Edit |  | 154,156,22,12 | 0x50810081 |
| 1 | Button(Default) | OK | 100,186,50,14 | 0x50010001 |
| 2 | Button | Cancel | 156,186,50,14 | 0x50010000 |

### DIALOG 174 「Property definition」

- サイズ 255x208、スタイル 0x90CC0040、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 1088 | SysListView32 |  | 7,6,81,159 | 0x5081C00D |
| 1051 | Button | Higher | 7,166,40,14 | 0x50010000 |
| 1052 | Button | Lower | 7,181,40,14 | 0x50010000 |
| 1166 | Button | New | 115,167,64,14 | 0x50010000 |
| 1053 | Button | Delete | 181,167,64,14 | 0x50010000 |
| 1 | Button | Save && Close | 115,187,64,14 | 0x50010000 |
| 2 | Button | Cancel | 181,187,64,14 | 0x50010000 |
| 1048 | Edit |  | 90,6,155,12 | 0x50810080 |
| 1172 | Static |  | 6,164,240,1 | 0x50000010 |
| -1 | Static | Filter: | 90,22,30,9 | 0x5002008C |
| 1165 | SysListView32 |  | 123,34,122,110 | 0x50834001 |
| 1089 | Static | Property: | 90,33,30,9 | 0x5002008C |
| 1171 | Static | More selecting detail items makes \nretrieving the files slower.  | 122,146,123,17 | 0x50020081 |
| 1055 | Button | Higher | 95,49,26,14 | 0x40010000 |
| 1056 | Button | Lower | 95,65,26,14 | 0x40010000 |
| 1057 | Edit |  | 123,20,122,12 | 0x50810080 |
| 1054 | Button | Modify Name | 49,167,64,14 | 0x40010000 |

### DIALOG 237 「General」

- サイズ 250x186、スタイル 0x80C800C0、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 1152 | Static | &Add to "Send to" Menu | 7,35,236,10 | 0x50020000 |
| 1150 | SysListView32 |  | 16,45,145,41 | 0x50814001 |
| 1149 | CheckBox | Folder &Context Menu | 7,7,236,9 | 0x50010003 |
| -1 | Static | Be able to launch "Flexible Renamer" from right-click-menu of folder. | 16,18,227,10 | 0x50020000 |
| -1 | Static | *In case of uninstall, uncheck all items. | 16,88,227,10 | 0x50020080 |
| 1178 | Static |  | 7,7,20,20 | 0x40000003 |
| -1 | Static |  | 7,32,236,1 | 0x50000010 |

### DIALOG 238 「Rename」

- サイズ 250x185、スタイル 0x80C800C0、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| 1175 | Static | Treatment when the name conflicted | 7,55,236,10 | 0x50020000 |
| 1063 | RadioButton | &Choose treatment in confliction of file name every time | 16,66,227,9 | 0x50020009 |
| 1064 | RadioButton | Auto &numbering using below setting | 16,76,227,9 | 0x50000009 |
| 1071 | Static | Format: | 27,89,28,9 | 0x58020000 |
| 1070 | Edit |  | 60,86,40,12 | 0x58810080 |
| 1073 | Static | Ex.: (??) | 103,89,29,9 | 0x58020000 |
| 1072 | Static | Position: | 27,100,28,9 | 0x58020000 |
| 1068 | RadioButton | S&tart | 59,100,56,9 | 0x58020009 |
| 1069 | RadioButton | &End | 59,109,56,9 | 0x58000009 |
| 1081 | Static | Start from: | 27,123,40,9 | 0x58020000 |
| 1065 | Edit |  | 71,119,48,12 | 0x58810082 |
| 1140 | msctls_updown32 |  | 119,119,10,13 | 0x580000B6 |
| 1083 | Static | Step: | 27,135,40,9 | 0x58020000 |
| 1067 | Edit |  | 71,132,48,12 | 0x58810082 |
| 1142 | msctls_updown32 |  | 119,132,10,13 | 0x580000B6 |
| 1074 | CheckBox | &Regist StrFunc.dll | 7,7,236,9 | 0x50010003 |
| -1 | Static | This is String operation DLL for script and supports StrConv/sprintf/LCMapString. | 17,18,226,18 | 0x50020000 |
| 1179 | Static |  | 7,7,20,20 | 0x40000003 |
| -1 | Static |  | 7,50,236,1 | 0x50000010 |
| -1 | Static | *In case of uninstall, uncheck this. | 17,36,226,10 | 0x50020000 |

### DIALOG 304 「」

- サイズ 186x9、スタイル 0x86000040、フォント Tahoma 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|

### DIALOG 30721 「新規」

- サイズ 183x70、スタイル 0x80C800C4、フォント MS UI Gothic 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|
| -1 | Static | 新規(&N)  | 6,5,123,8 | 0x50000000 |
| 100 | ListBox |  | 6,15,125,49 | 0x50A10001 |
| 1 | Button(Default) | OK | 137,6,40,14 | 0x50010001 |
| 2 | Button | キャンセル | 137,23,40,14 | 0x50010000 |
| 57670 | Button | ヘルプ(&H) | 137,43,40,14 | 0x50010000 |

### DIALOG 30734 「」

- サイズ 183x70、スタイル 0x80C800C8、フォント MS UI Gothic 9pt

| ID | クラス | テキスト | x,y,w,h | スタイル |
|---|---|---|---|---|

## メニュー

### MENU 64

- &File
  - E&xit\tAlt+F4 → コマンド 57665
- &Edit
  - &Undo\tCtrl+Z → コマンド 57643
  - ----
  - &Delete from List\tDel → コマンド 57632
  - Select &All\tCtrl+A → コマンド 57642
  - &Invert Selection\tCtrl+I → コマンド 32954
  - ----
  - Edit File/Folder &Name\tF2 → コマンド 32545
- &View
  - &Tool Bar → コマンド 59392
  - &Status Bar → コマンド 59393
  - ----
  - Specify File → コマンド 32953
  - Specify Folder → コマンド 32952
  - ----
  - &Rename → コマンド 65
  - &TagRename → コマンド 66
  - &Attribute → コマンド 67
  - &NumberedObject → コマンド 68
  - ----
  - &Font... → コマンド 32778
  - ----
  - Re&fresh folder tree\tF5 → コマンド 32985
- F&avorites
  - &Add Favorites\tto 20 Cases → コマンド 32920
  - &Organize Favorites... → コマンド 32921
  - ----
  - Favorites → コマンド 32922
  - Favorites → コマンド 32923
  - Favorites → コマンド 32924
  - Favorites → コマンド 32925
  - Favorites → コマンド 32926
  - Favorites → コマンド 32927
  - Favorites → コマンド 32928
  - Favorites → コマンド 32929
  - Favorites → コマンド 32930
  - Favorites → コマンド 32931
  - Favorites → コマンド 32932
  - Favorites → コマンド 32933
  - Favorites → コマンド 32934
  - Favorites → コマンド 32935
  - Favorites → コマンド 32936
  - Favorites → コマンド 32937
  - Favorites → コマンド 32938
  - Favorites → コマンド 32939
  - Favorites → コマンド 32940
  - Favorites → コマンド 32941
- &Tool
  - Reset Window &Layout → コマンド 32961
  - &Options... → コマンド 32984
- &Help
  - &Help → コマンド 57670
  - Flexible Renamer &Online → コマンド 32914
  - ----
  - &Language
    - Languages → コマンド 32963
  - ----
  - &About Flexible Renamer... → コマンド 57664

### MENU 135

- _WildSearch_
  - Ex.: *.??? → コマンド 32800
  - ----
  - Any Character\t? → コマンド 32801
  - Any String\t* → コマンド 32802
  - ----
  - Ask with E-mail → コマンド 32887
- _RegSearch_
  - Ex.: (.*)\.(...) → コマンド 32800
  - ----
  - an Any Character\t. → コマンド 32801
  - Any String\t.* → コマンド 32802
  - Within A to Z\t[A-Z] → コマンド 32803
  - Without A to Z\t[^A-Z] → コマンド 32804
  - a Numeric Character[0-9]\t\d → コマンド 32805
  - a Non-Numeric[^0-9]\t\D → コマンド 32806
  - an Alphanumeric Character[0-9a-zA-Z_]\t\w → コマンド 32807
  - a Non-Alphanumeric[^0-9a-zA-Z_]\t\W → コマンド 32808
  - a Space\t\s → コマンド 32809
  - a Non-Space\t\S → コマンド 32810
  - a Period\t\. → コマンド 32811
  - Start of Line\t^ → コマンド 32812
  - End of Line\t$ → コマンド 32813
  - Alternative\t\| → コマンド 32814
  - Zero or One\t? → コマンド 32815
  - Zero or More\t* → コマンド 32816
  - One or More\t+ → コマンド 32817
  - Exactly m Times\t{m} → コマンド 32818
  - at Least m Times\t{m,} → コマンド 32819
  - at Least m But Not More Than n Times\t{m,n} → コマンド 32820
  - Grouping/Tag\t() → コマンド 32821
  - ----
  - Ask with E-mail → コマンド 32887
- _TransSearch_
  - Ex.: a-zA-Z → コマンド 32800
  - ----
  - Lowercase\ta-z → コマンド 32822
  - Uppercase\tA-Z → コマンド 32823
  - Fullwidth Lowercase\tａ-ｚ → コマンド 32824
  - Fullwidth Uppercase\tＡ-Ｚ → コマンド 32825
  - ----
  - Hiragana\tぁ-ん → コマンド 32826
  - Katakana\tァ-ン → コマンド 32827
  - ----
  - Number\t0-9 → コマンド 32828
  - Fullwidth Number\t０-９ → コマンド 32829
  - Number Kanji\t〇一二三四五六七八九十 → コマンド 32830
  - Number Old-Kanji\t〇壱弐参四五六七八九拾 → コマンド 32831
  - Number Romen Lowercase\t０ⅰ-ⅹ → コマンド 32832
  - Number Romen Uppercase\t０Ⅰ-Ⅹ → コマンド 32833

### MENU 136

- _Replace_
  - Ex.: ??_Rename\2.\1 → コマンド 32840
  - ----
  - Current Name\t\0 → コマンド 32853
  - Current FileTitle\t\t → コマンド 32841
  - Current Extension\t\e → コマンド 32842
  - Folder Name\t\f → コマンド 32843
  - Parent Folder Name\t\F → コマンド 32844
  - 1-Digit Number\t? → コマンド 32845
  - 2-Digit Number\t?? → コマンド 32846
  - 3-Digit Number\t??? → コマンド 32847
  - 4-Digit Number\t???? → コマンド 32848
  - FileDate(4-Digit Year+Month+Day)\t\Y\m\d → コマンド 32849
  - FileDate(2-Digit Year+Month+Day)\t\y\m\d → コマンド 32850
  - FileTime(24Hour+Min.+Sec.)\t\H\M\S → コマンド 32851
  - FileTime(AM/PM+Hour+Min.+Sec.)\t\p\I\M\S → コマンド 32852
  - Match with Group 1\t\1 → コマンド 32854
  - Match with Group 2\t\2 → コマンド 32855
  - Match with Group 3\t\3 → コマンド 32856
  - Match with Group 4\t\4 → コマンド 32857
  - Match with Group 5\t\5 → コマンド 32858
  - Match with Group 6\t\6 → コマンド 32859
  - Match with Group 7\t\7 → コマンド 32860
  - Match with Group 8\t\8 → コマンド 32861
  - Match with Group 9\t\9 → コマンド 32862
  - Upper-Case Next One\t\u → コマンド 32865
  - Upper-Case Next Until \E\t\U → コマンド 32866
  - Lower-Case Next One\t\l → コマンド 32863
  - Lower-Case Next Until \E\t\L → コマンド 32864
  - Lapse Effect Of \U, \L, \Q\t\E → コマンド 32867
  - ----
  - Ask with E-mail → コマンド 32887
- _TransSearch_
  - Ex.: a-zA-Z → コマンド 32840
  - ----
  - Lowercase\ta-z → コマンド 32822
  - Uppercase\tA-Z → コマンド 32823
  - Fullwidth Lowercase\tａ-ｚ → コマンド 32824
  - Fullwidth Uppercase\tＡ-Ｚ → コマンド 32825
  - ----
  - Hiragana\tぁ-ん → コマンド 32826
  - Katakana\tァ-ン → コマンド 32827
  - ----
  - Number\t0-9 → コマンド 32828
  - Fullwidth Number\t０-９ → コマンド 32829
  - Number Kanji\t〇一二三四五六七八九十 → コマンド 32830
  - Number Old-Kanji\t〇壱弐参四五六七八九拾 → コマンド 32831
  - Number Romen Lowercase\t０ⅰ-ⅹ → コマンド 32832
  - Number Romen Uppercase\t０Ⅰ-Ⅹ → コマンド 32833

### MENU 137

- _Preset_
  - Preset → コマンド 32880
  - ----
  - &Add Current Search/Replace Setting → コマンド 32881
  - &Order Preset → コマンド 32882

### MENU 141

- _RegExOption_
  - Only Completely &Matched → コマンド 32903
  - &Case Sensitive → コマンド 32901

### MENU 170

- _Rename_
  - Recently Destination Folder → コマンド 32943
  - Recently Destination Folder → コマンド 32944
  - Recently Destination Folder → コマンド 32945
  - Recently Destination Folder → コマンド 32946
  - Recently Destination Folder → コマンド 32947
  - ----
  - Create &Subfolder at Destination → コマンド 32949

### MENU 171

- _Tag_
  - Tag → コマンド 32950
  - ----
  - Current Name\t\0 → コマンド 32853
  - Current FileTitle\t\t → コマンド 32841
  - Current Extension\t\e → コマンド 32842
  - Folder Name\t\f → コマンド 32843
  - Parent Folder Name\t\F → コマンド 32844
  - Other Format
    - 1-Digit Number\t? → コマンド 32845
    - 2-Digit Number\t?? → コマンド 32846
    - 3-Digit Number\t??? → コマンド 32847
    - 4-Digit Number\t???? → コマンド 32848
    - FileDate(4-Digit Year+Month+Day)\t\Y\m\d → コマンド 32849
    - FileDate(2-Digit Year+Month+Day)\t\y\m\d → コマンド 32850
    - FileTime(24Hour+Min.+Sec.)\t\H\M\S → コマンド 32851
    - FileTime(AM/PM+Hour+Min.+Sec.)\t\p\I\M\S → コマンド 32852
    - Upper-Case Next One\t\u → コマンド 32865
    - Upper-Case Next Until \E\t\U → コマンド 32866
    - Lower-Case Next One\t\l → コマンド 32863
    - Lower-Case Next Until \E\t\L → コマンド 32864
    - Lapse Effect Of \U, \L, \Q\t\E → コマンド 32867

### MENU 173

- _RenameOption_
  - &TagRename (DateTime Format && Invalid Characters) → コマンド 32951
  - ----
  - &Timestamp
    - &Updated Date as Timestamp → コマンド 32885
    - &Creation Date as Timestamp → コマンド 32886
  - ----
  - &Hide Protected Operating System File (Recommanded) → コマンド 32906

### MENU 181

- _ListHeader_
  - Show in groups
    - None → コマンド 32955
    - ----
    - In Folder → コマンド 32956
    - Date → コマンド 32957
    - Type → コマンド 32958
    - Extension → コマンド 32959
    - ----
  - ----
  - Shuffle → コマンド 32962
  - ----

### MENU 182

- _Script_
  - ----
  - Edit &Script... → コマンド 32967

### MENU 183

- _RenameOption_
  - &Numbering → コマンド 32917
  - ----
  - &Timestamp
    - &Updated Date as Timestamp → コマンド 32885
    - &Creation Date as Timestamp → コマンド 32886
  - ----
  - &Hide Protected Operating System File (Recommanded) → コマンド 32906

### MENU 303

- _Ctx_
  - Undo → コマンド 57643
  - Redo → コマンド 57644
  - ----
  - Cut → コマンド 57635
  - &Copy → コマンド 57634
  - Paste → コマンド 57637
  - ----
  - Select &All → コマンド 57642

### MENU 304

- _NumberingOption_
  - Decimal Number → コマンド 1
  - Hexadecimal Number → コマンド 2
  - Alphabetical Number → コマンド 3

### MENU 305

- &View
  - Re&fresh → コマンド 32985

## アクセラレータ

### ACCELERATOR 64

- Alt+F4 → コマンド 57665
- Delete → コマンド 57632
- Ctrl+I → コマンド 32954
- Ctrl+A → コマンド 57642
- Ctrl+Z → コマンド 57643
- Enter → コマンド 32776
- F5 → コマンド 32985

### ACCELERATOR 129

- Ctrl+C → コマンド 57634
- Ctrl+X → コマンド 57635
- F2 → コマンド 32545
- Ctrl+V → コマンド 57637
- Ctrl+A → コマンド 57644
- Ctrl+Z → コマンド 57643
- F5 → コマンド 32985

## 文字列テーブル

ID がコントロール ID と同じものはツールチップ、コマンド ID と同じものはステータスバーの説明（`
` の後ろがツールチップ）。

| ID | 文字列 |
|---|---|
| 64 | Flexible Renamer\n\nFlexib\n\n\nFlexibleRenamerEx.Document\nFlexib Document |
| 65 | Rename file/folder.\nRename |
| 66 | Rename file, using tag information.\nTagRename |
| 67 | Modify attributes/timestamps of file/folder.\nModify attributes |
| 68 | Create numbered object(Sized File/Folder), under the selected folder.\nCreate numbered object |
| 101 | &About Flexible Renamer |
| 102 | %m/%d/%Y %H:%M:%S |
| 103 | Rename Completed Successfully! |
| 104 | This file is used to UNDO, and Error recovery. |
| 105 | M/d/yyyy H:mm:ss |
| 106 | Flexible Renamer |
| 107 | Destination Name is in Collision!\n  Folder: %s\n  SourceName:  %s\n  DestinationName: %s\n\nSelect Disposal and Press Continue. |
| 108 | Failured to Rename!\n  Folder: %s\n  SourceName: %s\n  DestinationName: %s\n\nContinue? |
| 109 | Undo this operation?\nDate: %s\nFolder: %s\nSearch for: %s\nReplace with: %s |
| 110 | %s\nIllegal Log\nLast operation was probably abnormal terminated. |
| 111 | Undo Completed. |
| 112 | Select Target Folder |
| 113 | Rename Selected File/Folder Only |
| 114 | Destination file \n%s\nis already exist!\nRename was Canceled. |
| 115 | Rename All File/Folder |
| 116 | Failured to Undo |
| 117 | Skip This File and Continue |
| 118 | Skip All Error File and Continue |
| 119 | Abort and Undo Rename Operation |
| 120 | Abort Operation with Current Condition |
| 121 | Drag && Drop is also Available |
| 122 | Selection of Source File |
| 123 | Wildcard(*, ?) is Available |
| 124 | Wildcard(*, ?)-Matched Substrings Equals to \1-\9 |
| 125 | Invert Selection |
| 126 | Rename the files in the preview list |
| 127 | Undo last renaming operation |
| 129 | Rename Selected Files Only If Any, All Files If No Selection. |
| 130 | Recursive Under Subfolders |
| 131 | Start to Modify Attributes |
| 132 | Abort Operation |
| 133 | Failured to File Copy!\n%s |
| 134 | Modify Folder Timestamp |
| 135 | Flexible Renamer |
| 136 | Renaming... |
| 137 | Modifing Attributes... |
| 138 | Prepare to Modify... |
| 139 | Undo Renaming... |
| 140 | Current Name |
| 141 | New Name |
| 142 | %d Objects |
| 143 | %d Objects |
| 144 | Object Name |
| 145 | Object Size |
| 146 | Object Amount |
| 147 | Start to Create Objects |
| 148 | Creating Objects... |
| 149 | Create New Empty File |
| 150 | Create New Empty Folder |
| 151 | Search by Wildcard |
| 152 | Search by Regular-Expression |
| 153 | Regular-Expression is Available |
| 154 | Bracket-RegExp-Matched Substrings Equals to \1-\9 |
| 155 | Name |
| 156 | Size |
| 157 | Search Method |
| 158 | Search |
| 159 | Replace |
| 160 | Preset Name |
| 161 | Flexible Rename using Wildcard or Regular-Expression |
| 162 | Replace All Chars in Search List\nwith Equivalent Char in Replace List |
| 163 | mailto:naru@yeah.ne.jp |
| 164 | <Q>How to Search and Replace |
| 165 | Select Target Folder |
| 166 | No Target File |
| 167 | Changing the language is applied at the next starts. |
| 168 | Input Name |
| 169 | Input Size |
| 170 | Input Amount |
| 171 | Recall Already Defined SearchFor/ReplaceWith |
| 172 | Support to Search |
| 173 | Support to Replace |
| 174 | Options Concerning Rename |
| 175 | Sort by Condition and Renumber |
| 176 | Select Preset |
| 177 | There Objects will be Created |
| 178 | Can \'t use " |
| 179 | Type |
| 180 | Date Updated |
| 181 | Date Created |
| 182 | Date Accessed |
| 183 | Attribute |
| 184 | Modify Selected File/Folder Only |
| 185 | Meta-expressions are intactly treated as syntax of script. To deal as a string, put in double-quotations(Ex."\0"). |
| 186 | Displayed: %5d Objects |
| 187 | Selected: %5d Objects |
| 188 | Options Concerning Regular-Expression |
| 189 | Search is complete. There are no results to display. |
| 190 | Search is complete. There are no results to display. |
| 191 | Input Amount |
| 192 | Select Rename Function |
| 193 | Input Search for |
| 194 | Input Replace with |
| 195 | Wildcard |
| 196 | Regular-Expression |
| 197 | Letter-Translation |
| 198 | <Enter Additional String Here> |
| 199 | <Enter Source String Here> |
| 200 | <Enter Destination String Here> |
| 201 | Select Rename Function |
| 202 | Available to Settings Under the Function List |
| 203 | Folder List |
| 204 | Folder |
| 205 | File |
| 206 | Item selection |
| 207 | Function |
| 209 | Sorry, not implemented yet. |
| 210 | Modify to photo taken date of EXIF in case of exist |
| 211 | <Enter Name Here> |
| 212 | http://hp.vector.co.jp/authors/VA014830/english/FlexRena/ |
| 213 | Depth of the Subfolder to Search |
| 214 | <Select Source File for Copy> |
| 215 | <Select Source Folder for Copy> |
| 216 | Can define plural filter(wildcard), Separating by semicolon(;) |
| 217 | Copy the Existing File |
| 218 | Copy the Existing Folder |
| 219 | Select Source File for Copy |
| 220 | Select Source Folder for Copy |
| 221 | The following character cannot be used for a file name:\n\/:*?\"<>\| |
| 222 | Retry Operation |
| 223 | &Modify |
| 224 | Select Folder |
| 225 | Order Favorites |
| 226 | Skip |
| 227 | Process Selected File/Folder Only |
| 228 | Process All File/Folder |
| 229 | Select Destination Folder |
| 230 | Music |
| 231 | Picture |
| 232 | Image |
| 233 | Document |
| 234 | WebPage |
| 235 | Property |
| 236 | Script |
| 237 | http://hp.vector.co.jp/authors/VA014830/english/FlexRena/donation.htm |
| 240 | Target |
| 241 | Filter |
| 242 | Format |
| 243 | Rename |
| 244 | TagRename |
| 245 | Attribute |
| 246 | NumberedObject |
| 247 | Specify DateTime |
| 248 | Shift |
| 249 | Random Generate |
| 250 | Drop Files or Folders Here from Windows Explorer |
| 251 | Days to Shift |
| 252 | Time to Shift |
| 253 | Lower Limit of DateTime on Random-Generation |
| 254 | Upper Limit of DateTime on Random-Generation |
| 255 | No modification of attributes and timestamp |
| 256 | Format of New Name |
| 257 | List of Supported Tag-Informatoin |
| 258 | %m/%d/%Y |
| 259 | M/d/yyyy |
| 260 | Make the Same |
| 261 | Specify Folder |
| 262 | Specify File |
| 263 | In Folder |
| 264 | Searching... |
| 266 | Adding to List... |
| 268 | Although the objects were dropped, none were added to a list.\nCheck the "Target" and retry dropping. |
| 270 | The time format can also be specified. |
| 272 | A format can be edited. |
| 273 | Favorites |
| 274 | &Rename |
| 275 | &Move |
| 276 | &Copy |
| 277 | &Recycle Bin |
| 278 | &Shortcut |
| 279 | &HardLink |
| 282 | Rename |
| 283 | Move&Rename to Another Folder |
| 284 | Copy&Rename to Another Folder |
| 285 | Move to Recycle Bin |
| 286 | Create Shortcut&Rename to Another Folder |
| 287 | Create HardLink&Rename to Another Folder |
| 294 | Sequential Generate |
| 295 | Operation to Perform |
| 296 | Start Value of DateTime on Sequential-Generation |
| 297 | Min: |
| 298 | Start: |
| 299 | Searching %s |
| 300 | Search completed |
| 301 | New Preset |
| 302 | List of Supported Function |
| 303 | Edit property definition |
| 304 | Enter Advanced Rename mode with the selected menu item |
| 311 | Insert Number or String |
| 312 | Remove Number or String |
| 313 | Translate Letters |
| 314 | Replace String |
| 315 | Organize Number Value |
| 316 | Modify Extension |
| 500 | EN |
| 501 | Folder\shell\Flexible Renamer |
| 32545 | Edit file/folder name in the list directly\nEdit File/Folder Name |
| 32778 | Select Font\nFont |
| 32803 | [A-Z] |
| 32804 | [^A-Z] |
| 32805 | \d |
| 32806 | \D |
| 32807 | \w |
| 32808 | \W |
| 32809 | \s |
| 32810 | \S |
| 32811 | \. |
| 32812 | ^ |
| 32813 | $ |
| 32814 | \| |
| 32815 | ? |
| 32816 | * |
| 32817 | + |
| 32818 | {m} |
| 32819 | {m,} |
| 32820 | {m,n} |
| 32821 | () |
| 32822 | a-z |
| 32823 | A-Z |
| 32824 | ａ-ｚ |
| 32825 | Ａ-Ｚ |
| 32826 | ぁ-ん |
| 32827 | ァ-ン |
| 32828 | 0-9 |
| 32829 | ０-９ |
| 32830 | 〇一二三四五六七八九十 |
| 32831 | 〇壱弐参四五六七八九拾 |
| 32832 | ０ⅰ-ⅹ |
| 32833 | ０Ⅰ-Ⅹ |
| 32841 | \t |
| 32842 | \e |
| 32843 | \f |
| 32844 | \F |
| 32845 | ? |
| 32846 | ?? |
| 32847 | ??? |
| 32848 | ???? |
| 32849 | \Y\m\d |
| 32850 | \y\m\d |
| 32851 | \H\M\S |
| 32852 | \p\I\M\S |
| 32853 | \0 |
| 32854 | \1 |
| 32855 | \2 |
| 32856 | \3 |
| 32857 | \4 |
| 32858 | \5 |
| 32859 | \6 |
| 32860 | \7 |
| 32861 | \8 |
| 32862 | \9 |
| 32863 | \l |
| 32864 | \L |
| 32865 | \u |
| 32866 | \U |
| 32867 | \E |
| 32914 | Open Flexible Renamer Web Site\nFlexible Renamer Page |
| 32920 | Add Favorites |
| 32921 | Organize Favorites Folder |
| 32952 | Specify the targets by folder\nSpecify Folder |
| 32953 | Specify the target files by Drag & Drop\nSpecify File |
| 32954 | Reverses which are selected and which are not.\nInvert Selection |
| 32985 | Refresh folder tree\nRefresh |
| 32999 | Donate Flexible Renamer\nDonate |
| 57344 | Flexible Renamer |
| 57345 |   |
| 57632 | Deletes the selected items from the file list.\nDelete from List |
| 57642 | Selects all items in the file list.\nSelect All |
| 57643 | Undoes the previous rename operation.\nUndo |
| 57664 | Display program information, version number and copyright\nAbout |
| 57665 | Quits Flexible Renamer.\nExit |
| 59392 | Show or hide the toolbar\nToggle ToolBar |
| 59393 | Show or hide the status bar\nToggle StatusBar |
| 59394 |  Displayed: xxxxx Objects |
| 59395 |  Selected: xxxxx Objects |
| 61440 | 開く |
| 61441 | 名前を付けて保存 |
| 61442 | すべてのファイル (*.*) |
| 61443 | 無題 |
| 61446 | 無題のファイル |
| 61457 | 非表示(&H) |
| 61472 | エラー メッセージはありません。 |
| 61473 | サポートされていない操作を実行しようとしました。 |
| 61474 | 必要なリソースを利用できませんでした。 |
| 61475 | メモリが不足しています。 |
| 61476 | 不明なエラーが発生しました。 |
| 61477 | 引数が正しくありません。 |
| 61696 | ファイル名が正しくありません。 |
| 61697 | ドキュメントを開くことに失敗しました。 |
| 61698 | ドキュメントの保存に失敗しました。 |
| 61699 | %1 への変更を保存しますか? |
| 61700 | 空のドキュメントの作成に失敗しました。 |
| 61701 | ファイルが大きすぎて開けません。 |
| 61702 | 印刷ジョブを開始できませんでした。 |
| 61703 | ヘルプの起動に失敗しました。 |
| 61704 | アプリケーションの内部エラーです。 |
| 61705 | コマンドの実行に失敗しました。 |
| 61706 | メモリ不足のため実行できません。 |
| 61707 | システム レジストリ エントリは削除されています。また、INI ファイルがある場合は、INI ファイルも削除されました。 |
| 61708 | 一部のシステム レジストリ エントリ (または INI ファイル) は削除されませんでした。 |
| 61709 | このプログラムにはファイル %s が必要ですがこのシステム上に見つかりませんでした。 |
| 61710 | このプログラムは欠落エクスポート %s にファイル %s 内でリンクされています。このコンピューターは互換性のないバージョンの %s を含んでいる可能性があります。 |
| 61712 | 整数を入力してください。 |
| 61713 | 数字を入力してください。 |
| 61714 | %1 から %2 までの整数を入力してください。 |
| 61715 | %1 から %2 までの数字を入力してください。 |
| 61716 | %1 文字以内で入力してください。 |
| 61717 | ボタンを選択してください。 |
| 61718 | 0 から 255 までの整数を入力してください。 |
| 61719 | 正の整数を入力してください。 |
| 61720 | 日付/時刻を入力してください。 |
| 61721 | 通貨型を入力してください。 |
| 61722 | GUID を入力してください。 |
| 61723 | 時刻を入力してください。 |
| 61724 | 日付を入力してください。 |
| 61728 | 予期しないファイル形式です。 |
| 61729 | %1\nこのファイルが見つかりません。\nパスとファイル名が正しいかどうか確認してださい。 |
| 61730 | 出力先のディスク ドライブの空き容量が不足しています。 |
| 61731 | %1 は使用中のため、読み取れません。 |
| 61732 | %1 は使用中または読み取り専用のため、書き込めません。 |
| 61733 | %1 の読み取り中に予期しないエラーが発生しました。 |
| 61734 | %1 の書き込み中に予期しないエラーが発生しました。 |
| 61744 | %1: %2\nスクリプトの実行を続行しますか? |
| 61745 | ディスパッチの例外: %1 |
| 61836 | 書き込み専用プロパティからは読み取れません。 |
| 61837 | 読み取り専用プロパティには書き込めません。 |
| 61840 | メール システム サポートを読み込めません。 |
| 61841 | メール システム DLL が無効です。 |
| 61842 | メールの送信に失敗しました。 |
| 61856 | エラーはありませんでした。 |
| 61857 | %1 へのアクセス中に不明なエラーが発生しました。 |
| 61858 | %1 が見つかりませんでした。 |
| 61859 | %1 に正しくないパスが含まれています。 |
| 61860 | 開いているファイルが多すぎるため、%1 を開けませんでした。 |
| 61861 | %1 へのアクセスは拒否されました。 |
| 61862 | 正しくないファイル ハンドルが %1 に関連付けられていました。 |
| 61863 | 現在のディレクトリ %1 は削除できませんでした。 |
| 61864 | %1 を作成できませんでした。そのディレクトリはいっぱいです。 |
| 61865 | %1 のシークに失敗しました。 |
| 61866 | %1 へのアクセス中にハードウェア I/O エラーが報告されました。 |
| 61867 | %1 へのアクセス中に共有違反が発生しました。 |
| 61868 | %1 へのアクセス中にロック違反が発生しました。 |
| 61869 | %1 へのアクセス中にディスクがいっぱいになりました。 |
| 61870 | %1 の末尾以降にアクセスしようとしました。 |
| 61872 | エラーはありませんでした。 |
| 61873 | %1 へのアクセス中に不明なエラーが発生しました。 |
| 61874 | %1 の読み取り中に書き込もうとしました。 |
| 61875 | %1 の末尾以降にアクセスしようとしました。 |
| 61876 | %1 へ書き込み中に読み取ろうとしました。 |
| 61877 | %1 の形式が正しくありません。 |
| 61878 | %1 は予期しないオブジェクトを含んでいます。 |
| 61879 | %1 は不正なスキーマを含んでいます。 |
| 61888 | ピクセル |
| 62177 | オフ |
| 62178 | オン |
| 62179 | 混合 |
| 62180 | 1 つ以上の自動保存されたドキュメントが見つかりました。 |
| 62181 | これらのドキュメントは、現在開いているドキュメントより後に保存されています。また、アプリケーションの終了前に編集された変更内容も含まれています。 |
| 62182 | これらの自動保存されたドキュメントを復元しますか? |
| 62183 | 自動保存されたドキュメントを復元する場合、これらのドキュメントを手動で保存して元のドキュメントを上書きする必要があります。復元操作を実行しない場合、自動保存されたドキュメントは削除されます。 |
| 62184 | 自動保存されたドキュメントを復元する\n手動で保存したドキュメントではなく、自動保存されたドキュメントを開きます。 |
| 62185 | 自動保存されたドキュメントを復元しない\n最後に手動で保存したドキュメントを使用します。 |
| 62186 | %s [復元済み] |

## ツールバー

### TOOLBAR 64（ボタン 16x15）

コマンド: 32953, 32952, 1

### TOOLBAR 129（ボタン 16x15）

コマンド: 65, 66, 67, 68, 1
