--全局常量

local AppConfig = {}
local this = AppConfig


AppConfig.VERSION = '28'
AppConfig.ENV = AppConst.env

AppConfig.DEBUG = false


--- 埋点appid
AppConfig.DOTAPPID = "2b6b291ee62b4cdf9d91f9c7e48fca07"

--- 埋点上传地址
AppConfig.HTTPDOT = "https://g2020-shushu.laiyouxi.com/"

--- 神策上传地址
AppConfig.HTTPSC = "https://shence-data.laiyouxi.com/sa?project=default"

--- 登录专用
AppConfig.HTTPREGISTER = "https://intranet-2022jjmj-dev-register.laiyouxi.com:1443"

--- 游戏内api一般都调这个
AppConfig.HTTPOTHER = "https://intranet-2022jjmj-dev-other.laiyouxi.com:1443"

--- 目前只有拉ip时会用到，但是各服务地址都不同，所以也要写配置
AppConfig.HTTPIP = "https://intranet-2022jjmj-dev-nodef.laiyouxi.com:1443"

---SHARE_URL:    "https://g2021-online-share.ccmitc.com/share/?apkId=${apkId}&userId=${userId}&extra=${extra}"

AppConfig.Server_Ip = { "wss://intranet-2022jjmj-dev-gate.laiyouxi.com:1443/connect" }

--- 远程配置文件地址
AppConfig.REMOTE_RES = "https://g2022jjmj-cdn.laiyouxi.com/jjmj/remote-config-dev/"

--- 远程版本检测
AppConfig.CHECK_VER = "https://g2022jjmj-cdn.laiyouxi.com/jjmj/apk_jjmj/ver.json"

--- 不走resources中的加密
AppConfig.CDN_ROOT = "https://g2022jjmj-cdn.laiyouxi.com/jjmj"
AppConfig.CDN_PROJ = "https://g2022jjmj-cdn.laiyouxi.com/jjmj/apk_jjmj" --"https://g2022jjmj-cdn.laiyouxi.com/jjmj/apk_jjmj", "http://192.168.20.162:8000/apk_jjmj"

-- 渠道名称
AppConfig.channelName = 'apple'
-- 渠道id
AppConfig.channelId = 201
-- 默认apkid
AppConfig.default_apkid = 201
-- 渠道appId
AppConfig.channelAppId = "1"
-- deviceId
AppConfig.deviceId = "uuid"
-- 设备名称
AppConfig.deviceName = 'red mi'
-- 平台版本
AppConfig.versionName = "1.0.0"
-- 平台版本号
AppConfig.versionCode = 1

function AppConfig.ApkID()
    return this.default_apkid;
end

return AppConfig