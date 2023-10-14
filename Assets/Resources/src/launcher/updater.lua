local Updater = {}

local TAG = '[Updater]'

local function handler(obj, method)
    return function(...)
        return method(obj, ...)
    end
end


local application = UnityEngine.Application
-- local file = System.IO.File
local playerPrefs = UnityEngine.PlayerPrefs
local fileUtils = FileUtils.Instance

---创建更新器
---@param name string 热更模块
---@param storagePath string 存储路径
---@param localManifestUrl string 本地地址
---@param inAppManifest table 包内manifest
---@return table
function Updater.new(name, storagePath, localManifestUrl, inAppManifest)
    local instance = setmetatable({}, {__index = Updater})
	instance.class = Updater
	instance:ctor(name, storagePath, localManifestUrl, inAppManifest)
	return instance
end

function Updater:ctor(name, storagePath, localManifestUrl, inAppManifest)
    self._moduleKey = name
    -- 可写目录 eg. xxx/remote-hall
    local _tbl = {application.persistentDataPath, "/remote-", name};
    self._storagePath = table.concat(_tbl)
    self._localManifestUrl = localManifestUrl
    
    if inAppManifest == nil then
        self._inAppManifest = {
            packageUrl = "",
            remoteManifestUrl = "",
            remoteVersionUrl = "",
            version = "0",
            assets = {},
            searchPaths = {}
        }
    end

    -- 开始热更
    self.onStartUpdate = nil
    -- 热更进度
    self.onUpdateProgress = nil
    -- 热更失败
    self.onUpdateFailed = nil
    -- 热更成功
    self.onUpdateSuccess = nil
    -- 更新完毕
    self.onUpdateOver = nil

    -- 远程版本
    self._remoteVersion = -1
    -- 失败数目
    self._failCount = 0
    -- 热更下载器
    self._am = nil

    -- 下载进度
    self._progress = -1
    -- 全部文件
    self._totalFiles = 0
    -- 全部字节
    self._totalBytes = 0

    print(TAG .. " name:" .. name);
    print(TAG .. " storagePath:" .. storagePath);
    print(TAG .. " localManifestUrl:" .. localManifestUrl);
end

function Updater:getHotVersion()
    local content = playerPrefs.GetString(MODULE_VERSION, "{}")
    local tbl = ToTable(content)
    return tbl and tbl[self._moduleKey] or 0
end

function Updater:setHotVersion(v)
    local content = playerPrefs.GetString(MODULE_VERSION, "{}")
    local tbl = ToTable(content)
    tbl[self._moduleKey] = v

    playerPrefs.SetString(MODULE_VERSION, ToJson(tbl));
end

function Updater:setRemoteVersion(v)
    self._remoteVersion = v
end

function Updater:getRemoteVersion()
    return tonumber(self._remoteVersion)
end

function Updater:initAssetManager()
    self._am = Native.AssetsManager.New("", self._storagePath, handler(self, self.compareVersion))
    self._am:setMaxConcurrentTask(10)
end

---版本比较函数
---@param versionA number
---@param versionB number
---@return number >0 A大于B =0 A等于B <0 A小于B
function Updater:compareVersion(versionA, versionB)
    return tonumber(versionB) > tonumber(versionA) and -1 or 1;
end

---运行更新
---@param module string
---@param remoteVersion number
---@param remoteUrl string
function Updater:runTest(module, remoteVersion, remoteUrl)
    self:initAssetManager();
    -- self:createLocalDynamicManifest(module);
    self:setRemoteVersion(remoteVersion);
    self:prepareRemoteUrlAndCheckUpdate(remoteUrl);
end

---创建本地动态热更文件
---@param module string 模块名称
function Updater:createLocalDynamicManifest(module)
    local content = playerPrefs.GetString(MODULE_VERSION, "{}")

    local moduleVersions = ToTable(content)

    if moduleVersions[module] == nil then
        local manifestInfo = self._inAppManifest
        -- fileUtils:writeStringToFile(ToJson(manifestInfo), self._localManifestUrl)

        local inPackageVer = tonumber(manifestInfo.version)
        moduleVersions[module] = inPackageVer
        playerPrefs.SetString(MODULE_VERSION, ToJson(moduleVersions))

        print(string.format("[Updater] module:[%s] create manifest. version:{%s}.", module, inPackageVer))
    end
end

function Updater:prepareRemoteUrlAndCheckUpdate(remotePackageUrl)
    self._am:loadLocalManifest(self._localManifestUrl)
    
    local localManifest = self._am:getLocalManifest()

    if localManifest == nil or not localManifest:isLoaded() then
        self.onUpdateFailed(self._remoteVersion, self._progress)
    end

    local localVersion = localManifest:getVersion()
    local packageUrl = localManifest:getPackageUrl()
    local manifestRoot = localManifest:getManifestRoot()

    if packageUrl ~= remotePackageUrl then
        -- 同步本地manifest至最新服务器地址
        local newManifest = {
            packageUrl = remotePackageUrl,
            remoteManifestUrl = remotePackageUrl + "project.manifest",
            remoteVersionUrl = remotePackageUrl + "version.manifest",
            version = localVersion,
        }

        localManifest:parseJSONString(ToJson(newManifest), manifestRoot);

        print("[Updater] update local dynamic manifest to latest server url.")
    end

    self:checkUpdate()
end

function Updater:checkUpdate()
    self._am:setEventCallback(handler(self, self.checkCb))
    
    print('[Updater] check update.')

    self._am:checkUpdate()
end

function Updater:checkCb(event)
    local success = false
    local eventCode = event:getEventCode()

    if eventCode == Native.EventAssetsManager.ERROR_NO_LOCAL_MANIFEST then
        print('[Updater] checkCb() - No local manifest file found, hot update skipped.')
    elseif eventCode == Native.EventAssetsManager.ERROR_DOWNLOAD_MANIFEST then
        print('[Updater] checkCb() - Fail to download manifest file, hot update skipped.')
    elseif eventCode == Native.EventAssetsManager.ERROR_PARSE_MANIFEST then
        print('[Updater] checkCb() - Fail to parse manifest file, hot update skipped.')
    elseif eventCode == Native.EventAssetsManager.ALREADY_UP_TO_DATE then
        print('[Updater] checkCb() - Already up to date with the latest remote version.')
        success = true
        -- 本地有缓存版本比服务器高时 热更器加载本地缓存版本 会设置搜索路径
        local searchPaths = fileUtils:getSearchPaths()
        playerPrefs.SetString(SEARCH_PATHS, ToJson(searchPaths))
        local version = tonumber(self._am:getLocalManifest():getVersion())
        self:setHotVersion(version)
    elseif eventCode == Native.EventAssetsManager.NEW_VERSION_FOUND then
        print('[Updater] checkCb() - No local manifest file found, hot update skipped.')
        self._totalBytes = event:getTotalBytes()
        self._totalFiles = event:getTotalFiles()
        print(string.format("[Updater] checkCb() - New version found, please try to update. Total bytes:%s Total files:%s", self._totalBytes, self._totalFiles))
        success = true
    elseif eventCode == Native.EventAssetsManager.UPDATE_FINISHED then
        print('[Updater] checkCb() - checkCb() - Update finished.')
        self:updateSuccess();
        self._am:setEventCallback(nil)
        -- Prepend the manifest's search path
        local searchPaths = fileUtils:getSearchPaths()
        -- !!! Re-add the search paths in main.js is very important, otherwise, new scripts won't take effect.

        playerPrefs.SetString(SEARCH_PATHS, ToJson(searchPaths))
        fileUtils:setSearchPaths(searchPaths);

        self.onUpdateOver(true, self._remoteVersion);
    end

    if not success then
        self._am:setEventCallback(nil)
        self.onUpdateFailed(self._remoteVersion, self._progress)
    else
        if eventCode == Native.EventAssetsManager.ALREADY_UP_TO_DATE then
            -- 最新版本 正常启动
            self.onUpdateOver(true, self._remoteVersion)
        else
            self:startUpdate()
        end
    end
end

function Updater:startUpdate()
    self._failCount = 0
    
    print("[Updater] startUpdate() - Current state:", self._am:getState())

    -- 外部通知
    self.onStartUpdate()

    -- 更新设置
    self._am:setEventCallback(handler(self, self.updateCb))
    self._am:update()
end

function Updater:updateCb(event)
    -- 资源名称
    local assetId = event:getAssetId()
    -- 事件代码
    local eventCode = event:getEventCode()
    -- 事件信息
    local message = event:getMessage()
    -- CURL错误码
    local eCode = event:getCURLECode()
    -- CURL错误码
    local mCode = event:getCURLMCode()

    local needRestart = false
    local failed = false

    if eventCode == Native.EventAssetsManager.ERROR_NO_LOCAL_MANIFEST then
        print("startUpdate() - No local manifest file found, hot update skipped.")
        failed = true
    elseif eventCode == Native.EventAssetsManager.ERROR_DOWNLOAD_MANIFEST then
        print("startUpdate() - Fail to download manifest file, hot update skipped.")
        failed = true
    elseif eventCode == Native.EventAssetsManager.ERROR_PARSE_MANIFEST then
        print("startUpdate() - Fail to parse manifest file, hot update skipped.")
        failed = true
    elseif eventCode == Native.EventAssetsManager.UPDATE_PROGRESSION then
        self._progress = event:getPercent()

        local obj = {}
        obj.percent = self._progress;
        obj.downloadedFiles = event:getDownloadedFiles()
        obj.downloadedBytes = event:getDownloadedBytes()
        obj.totalFiles = self._totalFiles
        obj.totalBytes = self._totalBytes
        self.onUpdateProgress(obj)
    elseif eventCode == Native.EventAssetsManager.ASSET_UPDATED then
        print(string.format("startUpdate() - Asset[%s] updated.Download files:%s", assetId, self._am:getDownloadedFiles()))
    elseif eventCode == Native.EventAssetsManager.ERROR_UPDATING then
        -- 下载失败 解压失败 校验失败
        local totalFiles = self._am:getTotalFiles()
        local downloadedFiles = self._am:getDownloadedFiles()

        print(string.format("startUpdate() - Update failed. asset:%s error:%s curlE:%s curlM:%s.", assetId, message, eCode, mCode))
        print(string.format("startUpdate() - Update failed. Total files:%s Downloaded files:%s.", totalFiles, downloadedFiles))


    elseif eventCode == Native.EventAssetsManager.ERROR_DECOMPRESS then
        print("startUpdate() - Decompress error: ", message)
    elseif eventCode == Native.EventAssetsManager.ALREADY_UP_TO_DATE then
        print("startUpdate() - Already up to date with the latest remote version.")
        needRestart = true
    elseif eventCode == Native.EventAssetsManager.UPDATE_FINISHED then
        print("startUpdate() - Update finished. ", message)
        needRestart = true
        self:updateSuccess()
    elseif eventCode == Native.EventAssetsManager.UPDATE_FAILED then
        print(string.format("startUpdate() - Update failed. error:%s curlE:%s curlM:%s", message, eCode, mCode))
        self._failCount = self._failCount + 1

        failed = self._failCount >= 3

        if not failed then
            self:retry()
        end
    end

    if failed then
        self._am:setEventCallback(nil)
        self.onUpdateFailed(self._remoteVersion, self._progress)
    end

    if needRestart then
        self._am:setEventCallback(nil)
        -- Prepend the manifest's search path
        local searchPaths = fileUtils.getSearchPaths()
        -- !!! Re-add the search paths in main.js is very important, otherwise, new scripts won't take effect.
        local pathString = ToJson(searchPaths)
        playerPrefs.SetString(SEARCH_PATHS, pathString)
        fileUtils:setSearchPaths(searchPaths)
        print("Update over. Search paths: ", pathString)

        self.onUpdateOver(true, self._remoteVersion);
    end
end

function Updater:updateSuccess()
    print("-----updateSuccess-----")
    self.onUpdateSuccess()

    -- 设置版本
    local newVersion = self._remoteVersion;

    if newVersion  == -1 then
        newVersion = tonumber(self._am:getLocalManifest():getVersion())
    end

    self:setHotVersion(newVersion)
end

function Updater:retry()
    self._am:downloadFailedAssets()
    local totalFiles = self._am:getTotalFiles()

    print(string.format("%s times retry download failed %s Assets...", self._failCount, totalFiles))
end

return Updater