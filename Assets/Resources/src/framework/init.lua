print("===========================================================")
print("                        LOAD CORE")
print("===========================================================")
print("lua version " .. _VERSION)

-- 0 - 禁止日志, 1 - error | warn, 2 - log
U3D_DEBUG = 1

u3d = u3d or {}

function u3d.setmethods(target, component, methods)
    for _, name in ipairs(methods) do
        local method = component[name]
        target[name] = function(__, ...)
            return method(component, ...)
        end
    end
end

function u3d.unsetmethods(target, methods)
    for _, name in ipairs(methods) do
        target[name] = nil
    end
end

--- debug: 调试接口
--- functions: 提供一组常用的函数，以及对 Lua 标准库的扩展
--- device: 针对设备接口的扩展
--- json: JSON 的编码和解码接口
--- GNet: websocket网络接口
--- GEvent: 全局事件接口
require("framework.debug")
require("framework.functions")

device = require("framework.device")
local cjson = require("framework.json")
if cjson then
    json = cjson
else
    printError("没有找到cjson库")
end


--- ui路由器
Router = require('framework.ui.Router')
--- ui管理器
GUI = require('framework.ui.UIManager')
GEvent = require('framework.event').new()
GNet = require('framework.network.GNet')