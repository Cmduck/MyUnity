
local CallbackHelper = class('CallbackHelper')

function CallbackHelper:ctor()
    self.autoId = 0
    self.callbackMap = {}
end

function CallbackHelper:saveCallback(key, cb)
    if lua_str_isNullOrEmpty(key) then
        self.autoId = self.autoId + 1
        key = self.autoId
    end

    if self.callbackMap[key] == nil then
        self.callbackMap[key] = {}
    end

    table.insert(self.callbackMap[key], cb)

    return key
end

function CallbackHelper:dealCallback(key, ...)
    local list = self.callbackMap[key]

    if list ~= nil then
        for i = 1, #list do
            list[i](...)
        end
    end

    self.callbackMap[key] = nil
end

function CallbackHelper:clearCallback(key)
    if lua_str_isNullOrEmpty(key) then
        self.callbackMap = {}
    else
        self.callbackMap[key] = nil
    end
end

function CallbackHelper:containKey(key)
    if self.callbackMap[key] then
        return true
    end

    return false
end

return CallbackHelper