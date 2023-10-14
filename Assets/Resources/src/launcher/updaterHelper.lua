local Updater = require("launcher.updater")
local control = require("launcher.control")
local file = System.IO.File

local UnityWebRequest = UnityEngine.Networking.UnityWebRequest

local co = coroutine

local function coSyncHttpGet(url)
    local www = UnityWebRequest.Get(url)
    www:SendWebRequest()

    while not www.isDone do
    end

    return www.downloadHandler.text
end


local UpdaterHelper = {}

local application = UnityEngine.Application;

local function handler(obj, method)
    return function(...)
        return method(obj, ...)
    end
end

Updater.EVENT_UPDATER = {
    --[[
        业务事件
    --]]
    -- 获取配置失败
    EVENT_GET_CONFIG_FAILED = "EVENT_GET_CONFIG_FAILED",
    -- 服务器维护
    EVENT_SERVER_MAINTAIN = "EVENT_SERVER_MAINTAIN",
    -- 强制更新
    EVENT_FORCE_UPDATE = "EVENT_FORCE_UPDATE",
    -- 准备更新
    EVENT_PREPARE_UPDATE = "EVENT_PREPARE_UPDATE",

    --[[
        热更事件
    --]]
    -- 开始热更
    EVENT_START_UPDATE = "EVENT_START_UPDATE",
    -- 热更进度
    EVENT_UPDATE_PROGRESS = "EVENT_UPDATE_PROGRESS",
    -- 热更失败
    EVENT_UPDATE_FAILED = "EVENT_UPDATE_FAILED",
    -- 热更成功
    EVENT_UPDATE_SUCCESS = "EVENT_UPDATE_SUCCESS",
    -- 更新完毕
    EVENT_UPDATE_OVER = "EVENT_UPDATE_OVER",
}

---创建更新辅助
---@param name string 热更模块
---@return table
function UpdaterHelper.new(name, panel)
    local instance = setmetatable({}, {__index = UpdaterHelper})
	instance.class = UpdaterHelper
	instance:ctor(name, panel)
	return instance
end

function UpdaterHelper:ctor(name, panel)
    self._name = name
    self._panel = panel
    -- 可写目录 eg. xxx/remote-hall
    local _tbl = {application.persistentDataPath, "/remote-", name}     
    self._storagePath = table.concat(_tbl)          -- persistentDataPath/remote-hall
    self._localManifestUrl = application.persistentDataPath .. "/" .. name .. "_manifest.json"  -- -- persistentDataPath/hall_manifest.json

    local content = file.ReadAllText(self._localManifestUrl)
    self._inAppManifest = ToTable(content)
    self._updater = nil
end

---创建更新器
function UpdaterHelper:createUpdater()
    self._updater = Updater.new(self._name, self._storagePath, self._localManifestUrl, self._inAppManifest)
    local updater = self._updater
    local panel = self._panel
    updater:initAssetManager()

    updater.onStartUpdate = handler(panel, panel.onStartUpdate)
    updater.onUpdateProgress = handler(panel, panel.onUpdateProgress)
    updater.onUpdateFailed = handler(panel, panel.onUpdateFailed)
    updater.onUpdateSuccess = handler(panel, panel.onUpdateSuccess)
    updater.onUpdateOver = handler(panel, panel.onUpdateOver)
end

---是否服务器维护
---@param errId number
function UpdaterHelper:isServerMaintenance(errId)
    return errId > 0
end

---是否需要强更
---@param forceVerison number
function UpdaterHelper:isNeedForceUpdate(forceVerison)
    -- TODO 强更判断
    return false
end

---是否需要热更
---@param hotVersion number
function UpdaterHelper:isNeedHotUpdate(hotVersion)
    -- unity项目不同于creator,unity新包走会走解压流程(初始包就有缓存版本),意味着untiy项目不用像creator一样有三方版本比较(包内版本,缓存版本,远程版本) unity仅需要比较(缓存版本和远程版本)

    local inAppVersion = self._inAppManifest.version
    local localVersion = self._updater:getHotVersion()

    if localVersion < hotVersion then
        -- 需要更新
        self._panel:onPrepareUpdate(self._name, hotVersion)

        -- 热更描述
        local urlTbl = {GConf.cdnEnv(), "/apk_201/", self._name, "_", hotVersion, ".json"}
        local url = table.concat(urlTbl)
        print('热更描述地址:', url)

        local thread = co.create(coSyncHttpGet)
        local status, content = co.resume(thread, url)

        if status then
            if content == nil or content == "" then
                print('#总控阶段# 获取热更描述失败')
                self._panel.onGetConfigFailed()
            else
                print("#总控阶段# 热更信息:", content)
                local rsp = ToTable(content)

                local res_path = '/res';

                if Device.Platform == "ios" then
                    res_path = '/res-ios';
                end

                local hotUrlTab = {GConf.cdnProj, res_path, '/', self._name, '/', hotVersion, '/'}
                local hotupdateUrl = table.concat(hotUrlTab)

                print('#总控阶段# 热更地址:', hotupdateUrl)
                if rsp and rsp.url and rsp.url ~= '' then
                    hotupdateUrl = rsp.url
                end

                -- 口令 TODO

                self._updater:setRemoteVersion(hotVersion);
                self._updater:prepareRemoteUrlAndCheckUpdate(hotupdateUrl);

                return true
            end
        else
            error(string.format("请求%s异常", url))
        end

        return false
    end

    return false
end

---启动更新
function UpdaterHelper:start()
    local panel = self._panel

    self._updater:createLocalDynamicManifest(self._name)

    coroutine.start(control.init, function (err)
        if err then
            panel.onGetConfigFailed("#总控阶段# 请求总控失败")
            return 
        end

        if control[self._name] == nil then
            print('#总控阶段# 总控发布异常 缺少字段:' .. self._name)
            return
        end
        
        local ser_err = control.h5_ser_err
        local update = control.update
        local hotver = control[self._name]

        -- 特殊指定版本 TODO
        
        -- 口令 TODO

        print('检测是否服务器维护')
        if self:isServerMaintenance(ser_err) == true then
            panel.onServerMaintain(ser_err)
            return
        end

        print('检测是否强更')
        if self:isNeedForceUpdate(update) == true then
            return
        end

        print('检测是否热更')
        if self:isNeedHotUpdate(hotver) == true then
            return
        end

        panel.onUpdateOver(false)
    end)

    print("#########")
end

return UpdaterHelper