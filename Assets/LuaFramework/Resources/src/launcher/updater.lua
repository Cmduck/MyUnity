local Updater = {}

local TAG = '[Updater]'

local application = UnityEngine.Application;

---创建更新器
---@param name string 热更模块
---@param storagePath string 存储路径
---@param localManifestUrl string 本地地址
---@param inAppManifest table 包内manifest
---@return table
function Updater.new(name, storagePath, localManifestUrl, inAppManifest)
    local instance = setmetatable({}, Updater)
	instance.class = Updater
	instance:ctor(name, storagePath, localManifestUrl, inAppManifest)
	return instance
end

function Updater:ctor(name, storagePath, localManifestUrl, inAppManifest)
    self._moduleKey = name
    -- 可写目录 eg. xxx/remote-hall
    local _tbl = {application.persistentDataPath, "/remote-", name};
    self._storagePath = table.concat(_tbl)
    self._localManifestUrl = ""
    
    if inAppManifest == nil then
        this._inAppManifest = {
            packageUrl = "",
            remoteManifestUrl = "",
            remoteVersionUrl = "",
            version = "0",
            assets = {},
            searchPaths = {}
        }
    end

    print(TAG .. " name:" .. name);
    print(TAG .. " storagePath:" .. storagePath);
    print(TAG .. " localManifestUrl:" .. localManifestUrl);
end

return Updater