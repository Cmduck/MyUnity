local UpdaterPanel = require('launcher.updaterPanel')

local Updater = require("launcher.updater")
local UpdaterHelper = {}

local application = UnityEngine.Application;

local EVENT_UPDATER = {
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
function UpdaterHelper.new(name)
    local instance = setmetatable({}, UpdaterHelper)
	instance.class = UpdaterHelper
	instance:ctor(name)
	return instance
end

function UpdaterHelper:ctor(name)
    self._name = name
    -- 可写目录 eg. xxx/remote-hall
    local _tbl = {application.persistentDataPath, "/remote-", name};
    self._storagePath = table.concat(_tbl)
    self._localManifestUrl = ""
    self._inAppManifest = nil
end

---创建更新器
function UpdaterHelper:createUpdater()
    
end

---是否服务器维护
---@param errId number
function UpdaterHelper:isServerMaintenance(errId)
    
end

---是否需要强更
---@param forceVerison number
function UpdaterHelper:isNeedForceUpdate(forceVerison)
    
end

---是否需要热更
---@param hotVersion number
function UpdaterHelper:isNeedHotUpdate(hotVersion)
    
end

function UpdaterHelper:start()
    
end