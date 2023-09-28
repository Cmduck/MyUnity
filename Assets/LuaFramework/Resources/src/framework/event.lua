
local Event = class("Event")

local EXPORTED_METHODS = {
    "on",
    "emit",
    "offHandle",
    "offTag",
    "off",
    "offAll",
    "has",
    "dump",
}

function Event:ctor()
    self:init_()
end

--- 初始化
function Event:init_()
    self.target_ = nil
    self.listeners_ = {}
    self.nextListenerHandleIndex_ = 0
end

--- 绑定对象(组件化使用)
---@param target any
function Event:bind(target)
    self:init_()
    u3d.setmethods(target, self, EXPORTED_METHODS)
    self.target_ = target
end

--- 解绑对象(组件化使用)
function Event:unbind(target)
    u3d.unsetmethods(target, EXPORTED_METHODS)
    self:init_()
end

--- 监听事件
---@param eventName string      事件名称
---@param listener function     事件回调
---@param tag  string | integer 事件标识(可选)
---@return any
---@return string
function Event:on(eventName, listener, tag)
    assert(type(eventName) == "string" and eventName ~= "",
        "Event:on() - invalid eventName")

    if self.listeners_[eventName] == nil then
        self.listeners_[eventName] = {}
    end

    self.nextListenerHandleIndex_ = self.nextListenerHandleIndex_ + 1
    local handle = tostring(self.nextListenerHandleIndex_)
    tag = tag or ""
    self.listeners_[eventName][handle] = {listener, tag}

    if U3D_DEBUG > 1 then
        printInfo("%s [Event] on() - event: %s, handle: %s, tag: \"%s\"",
                  tostring(self.target_), eventName, handle, tostring(tag))
    end

    return self.target_, handle
end


--- 发送事件
---@param event table 事件名  {name: 事件名, pass?: 传参}
---@return any
function Event:emit(event)
    local eventName = event.name
    if U3D_DEBUG > 1 then
        printInfo("%s [Event] emit() - event %s", tostring(self.target_), eventName)
    end

    if self.listeners_[eventName] == nil then return end
    event.target = self.target_
    event.stop_ = false
    event.stop = function(self)
        self.stop_ = true
    end

    for handle, listener in pairs(self.listeners_[eventName]) do
        if U3D_DEBUG > 1 then
            printInfo("%s [Event] emit() - dispatching event %s to listener %s", tostring(self.target_), eventName, handle)
        end

        event.tag = listener[2]
        listener[1](event)
        if event.stop_ then
            if U3D_DEBUG > 1 then
                printInfo("%s [Event] emit() - break dispatching for event %s", tostring(self.target_), eventName)
            end
            break
        end
    end

    return self.target_
end

---移除指定handle的事件
---@param handleToRemove string 
---@return any
function Event:offHandle(handleToRemove)
    for eventName, listenersForEvent in pairs(self.listeners_) do
        for handle, _ in pairs(listenersForEvent) do
            if handle == handleToRemove then
                listenersForEvent[handle] = nil
                if U3D_DEBUG > 1 then
                    printInfo("%s [Event] offHandle() - remove listener [%s] for event %s", tostring(self.target_), handle, eventName)
                end
                return self.target_
            end
        end
    end

    return self.target_
end

---移除指定tag事件
---@param tagToRemove string | integer
---@return any
function Event:offTag(tagToRemove)
    for eventName, listenersForEvent in pairs(self.listeners_) do
        for handle, listener in pairs(listenersForEvent) do
            -- listener[1] = listener
            -- listener[2] = tag
            if listener[2] == tagToRemove then
                listenersForEvent[handle] = nil
                if U3D_DEBUG > 1 then
                    printInfo("%s [Event] offHandle() - remove listener [%s] for event %s", tostring(self.target_), handle, eventName)
                end
            end
        end
    end

    return self.target_
end

---移除指定事件
---@param eventName any
---@return any
function Event:off(eventName)
    self.listeners_[eventName] = nil
    if U3D_DEBUG > 1 then
        printInfo("%s [Event] removeAllEventListenersForEvent() - remove all listeners for event %s", tostring(self.target_), eventName)
    end
    return self.target_
end

---移除所有事件
---@return any
function Event:offAll()
    self.listeners_ = {}
    if U3D_DEBUG > 1 then
        printInfo("%s [Event] offAll() - remove all listeners", tostring(self.target_))
    end
    return self.target_
end

--- 事件名是否存在
---@param eventName string
---@return boolean
function Event:has(eventName)
    local t = self.listeners_[eventName]
    for _, __ in pairs(t) do
        return true
    end
    return false
end

---打印所有事件
---@return any
function Event:dump()
    print("---- Event:dump() ----")
    for name, listeners in pairs(self.listeners_) do
        printf("-- event: %s", name)
        for handle, listener in pairs(listeners) do
            printf("--     listener: %s, handle: %s", tostring(listener[1]), tostring(handle))
        end
    end
    return self.target_
end

return Event