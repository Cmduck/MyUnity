--------------------------------
-- @module device

--[[--

提供设备相关属性的查询，以及设备功能的访问

当框架初始完成后，device 模块提供下列属性：

-   device.platform 返回当前运行平台的名字，可用值： ios, android, mac, windows.
-   device.model 返回设备型号，可用值： unknown, iphone, ipad
-   device.writablePath 返回设备上可以写入数据的首选路径：
    -   iOS 上返回应用程序所在的 Documents 目录
    -   Android 上返回存储卡的根目录
    -   其他平台的返回值由 quick-x-player 决定
-   device.cachePath 返回设备上可以写入数据的缓存目录：
    -   iOS 上返回应用程序所在的 Library/Caches 目录
    -   其他平台的返回值同 device.writablePath
-   device.directorySeparator 目录分隔符，在 Windows 平台上是 “\”，其他平台都是 “/”
-   device.pathSeparator 路径分隔符，在 Windows 平台上是 “;”，其他平台都是 “:”

]]

local deviceImpl = Device

local device = {}

--- 设备平台(windows, ios, mac, android, unknown)
device.platform     = deviceImpl.Platform
--- 设备型号(iPhone, iPad , ...)
device.model        = deviceImpl.Model
--- 操作系统
device.system       = deviceImpl.System
--- 设备名称
device.name         = deviceImpl.Name
--- 设备唯一标识
device.uid          = deviceImpl.Uid

device.isMobile     = deviceImpl.IsMobile

device.isEditor     = deviceImpl.IsEditor

function device.clipboard()
    return deviceImpl.Clipboard;
end


printInfo("# device.platform = " .. device.platform)
printInfo("# device.model = " .. device.model)
printInfo("# device.system = " .. device.system)
printInfo("# device.name = " .. device.name)
printInfo("# device.uid = " .. device.uid)
printInfo("# device.isMobile = " .. tostring(device.isMobile))
printInfo("# device.isEditor = " .. tostring(device.isEditor))

printInfo("# Application.dataPath = " .. Application.dataPath)
printInfo("# Application.persistentDataPath = " .. Application.persistentDataPath)
printInfo("# Application.consoleLogPath = " .. Application.consoleLogPath)
printInfo("# Application.streamingAssetsPath = " .. Application.streamingAssetsPath)
printInfo("# Application.temporaryCachePath = " .. Application.temporaryCachePath)

printInfo("# Util.DataPath = " .. Util.DataPath)
printInfo("# Util.TempDataPath = " .. Util.TempDataPath)
printInfo("# Util.AppContentPath = " .. Util.AppContentPath)
printInfo("# Util.AppContentPathURL = " .. Util.AppContentPathURL)
printInfo("#")

return device
