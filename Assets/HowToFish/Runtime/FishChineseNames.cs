namespace HowToFish {
    // Stable source IDs keep localization independent of art filenames and saves.
    public static class FishChineseNames {
        public static string Get(int originalId,string fallback="未知鱼类"){
            switch(originalId){
                case 0:return "神仙鱼";
                case 1:return "鮟鱇鱼";
                case 2:return "黑鲈";
                case 3:return "水滴鱼";
                case 4:return "蓝鳃太阳鱼";
                case 6:return "蓝鲨";
                case 7:return "弓头鲸";
                case 8:return "远古狗鱼";
                case 9:return "巨型食人鱼";
                case 10:return "哥布林鲨";
                case 12:return "河豚";
                case 13:return "蜘蛛蟹";
                case 14:return "翻车鱼";
                case 15:return "金枪鱼";
                case 16:return "碗鱼";
                case 17:return "鲶鱼";
                case 19:return "小丑鱼";
                case 20:return "鳕鱼";
                case 21:return "褐蟹";
                case 22:return "潮流鱼";
                case 23:return "鳗鱼";
                case 24:return "飞鱼";
                case 26:return "雀鳝";
                case 27:return "虾虎鱼";
                case 28:return "金鱼";
                case 29:return "大比目鱼";
                case 32:return "龙虾";
                case 33:return "鲭鱼";
                case 34:return "针鱼";
                case 35:return "皇带鱼";
                case 36:return "鹦嘴鱼";
                case 37:return "河鲈";
                case 38:return "狗鱼";
                case 39:return "食人鱼";
                case 40:return "红笛鲷";
                case 41:return "鲑鱼";
                case 42:return "海胆";
                case 44:return "海马";
                case 45:return "森加拉特鱼";
                case 46:return "虾";
                case 47:return "石头鱼";
                case 48:return "超级迷你鱼";
                case 49:return "虎鱼";
                case 50:return "扳机鱼";
                case 51:return "体素鱼";
                case 52:return "黄箱鲀";
                case 79:return "石蟹";
                case 80:return "宾邦怪鱼";
                case 88:return "鱿鱼";
                case 89:return "海星";
                case 90:return "海绵";
                case 94:return "丁鱥";
                case 95:return "狭鳕";
                case 96:return "鲯鳅";
                case 97:return "鰤鱼";
                case 98:return "红点鲑";
                case 99:return "狗鲨";
                case 100:return "三脚架鱼";
                case 101:return "锦鲤";
                case 102:return "鲤鱼";
                default:return fallback;
            }
        }
    }
}
