
local WebSocket = {}

-- 网络实现 重要!!! 所有调用原生C#方法都必须是冒号形式
local impl = LuaHelper.GetNetManager()


---开始连接
---@param url any       服务地址
---@param timeout any   超时时间(默认30秒)
function WebSocket.Connect(url, timeout)
	timeout = timeout or 30
    impl:Connect(url, timeout)
end

function WebSocket.Send(buf)
    impl:Send(buf)
end

--关闭连接
function WebSocket.Close()
    impl:Close()
end

return WebSocket