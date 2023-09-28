local updater = require('launcher.updater')

local Launcher = {}

---创建Launcher
---@param mode number 0 换包 1 热更
---@param meta table  整包元信息
---@return table
function Launcher.new(mode, meta)
    local instance = setmetatable({}, Launcher)
	instance.class = Launcher
	instance:ctor(mode, meta)
	return instance
end

function Launcher:ctor(mode, meta)
    self.mode = mode
    self.meta = meta
end

---执行启动器
---@param checkNewUpdatePackage boolean 是否检测新的启动包
function Launcher:run(checkNewUpdatePackage)
    local newUpdatePackage = updater.hasNewUpdatePackage()
	print(string.format("Launcher.run(%s), newUpdatePackage:%s", checkNewUpdatePackage, newUpdatePackage))

	if  checkNewUpdatePackage and newUpdatePackage ~= nil then
        -- Launcher模块脚本有更新 先更新自身
		self:updateSelf(newUpdatePackage)
	elseif updater.checkUpdate() then
		self:runUpdateScene(function()
			_G["finalRes"] = updater.getResCopy()
			self:runRootScene()
		end)
	else
		_G["finalRes"] = updater.getResCopy()
		self:runRootScene()
	end
end

---加载新的launcher包
---@param newUpdatePackage string
function Launcher:updateSelf(newUpdatePackage)
	print("Launcher.updateSelf ", newUpdatePackage)

	local launcherPackage = {
		"launcher.launcher",
		"launcher.updater",
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