local UpdaterPanel = require('launcher.updaterPanel')
local Launcher = {}

---创建Launcher
---@param mode number 0 换包 1 热更
---@param meta table  整包元信息
---@return table
function Launcher.new(mode, meta)
    local instance = setmetatable({}, {__index = Launcher})
	instance.class = Launcher
	instance:ctor(mode, meta)
	return instance
end

function Launcher:ctor(mode, meta)
    self.mode = mode
    self.meta = meta
	self.panel = nil
end

---执行启动器
---@param checkNewUpdatePackage boolean 是否检测新的启动包
function Launcher:run(checkNewUpdatePackage)
    local newUpdatePackage = self:hasNewUpdatePackage()
	print(string.format("Launcher.run(%s), newUpdatePackage:%s", checkNewUpdatePackage, newUpdatePackage))

	if  checkNewUpdatePackage and newUpdatePackage ~= nil then
        -- Launcher模块脚本有更新 先更新自身
		print('更新launcher')
		self:updateSelf(newUpdatePackage)
	else
		UpdaterPanel.run(self.meta)
	end
end

function Launcher:hasNewUpdatePackage()
	local zpath = UnityEngine.Application.persistentDataPath .. "/remote-hall/src/launcher.zip";

	if System.IO.File.Exists(zpath) then
		return zpath
	end

	return nil
end

---加载新的launcher包
---@param newUpdatePackage string
function Launcher:updateSelf(newUpdatePackage)
	print("Launcher.updateSelf ", newUpdatePackage)

	local launcherPackage = {
		"launcher.conf",
		"launcher.control",
		"launcher.launcher",
		"launcher.updater",
		"launcher.updaterHelper",
		"launcher.updaterPanel"
	}
	self:_printPackages("--before clean")
	for __,v in ipairs(launcherPackage) do
		package.preload[v] = nil
		package.loaded[v] = nil
	end
	self:_printPackages("--after clean")
	_G["update"] = nil
	CCLuaLoadChunksFromZIP(newUpdatePackage)
	self:_printPackages("--after CCLuaLoadChunksForZIP")
    require("launcher.launcher").new():run(false)
	self:_printPackages("--after require and run")
end

function Launcher:_printPackages(label)
	label = label or ""
	print("\npring packages "..label.."------------------")
	for __k, __v in pairs(package.preload) do
		print("package.preload:", __k, __v)
	end
	for __k, __v in pairs(package.loaded) do
		print("package.loaded:", __k, __v)
	end
	print("print packages "..label.."------------------\n")
end

function Launcher:runRootScene()
    require('app.MyApp').run()
end

return Launcher