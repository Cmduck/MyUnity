
require('framework.network.IMessage')
local CallbackHelper = require('framework.CallbackHelper')
local ws = require('framework.network.WebSocket')

NetEvents = {
    OnSocketConnected = "OnSocketConnected",
    OnSocketTimeout = "OnSocketTimeout",
    OnSocketError = "OnSocketError",
    OnSocketClose = "OnSocketClose",
    OnSocketData = "OnSocketData",


    on_deal_hallmsg = "on_deal_hallmsg",
    on_deal_gamemsg = "on_deal_gamemsg",
}

local GNet = {}
local this = GNet

-- 消息回调管理对象
local cbHelper = CallbackHelper.new()

-- 连接成功回调
local connectedHandler = nil

-- 上次收消息时间
local lastTime = 0

-- 最大等待时间 毫秒
local maxTime = 15000

-- 服务器心跳时间
local heartTime = 15

-- 缓存消息数据
local msgCache = {}

-- 消息id 每条消息自增 方便数据追踪 bug 
local msgAutoID = 0
-- 消息头的UID
local msgHeadUid = 0
-- 消息头的token
local msgHeadToken = 0
-- 大厅消息解码 --
local hallDecoder = nil
-- 游戏消息解码 --
local gameDecoder = nil


function CreateMessageHead()
    local HeadSize = 52
    -- 1-消息头的标识
    local HeadPad = 36
    -- 1-消息头结束标识
    local EndPad = 36

    local head = {}
    -- 1-消息内容格式 1json串 2二进制
    head.TM = 0
    -- 1-请求类型 0-push 1-request
    head.RT = 0
    -- 1-预留位置
    head.Free1 = 0
    -- 8-消息唯一id
    head.Id = 0
    -- 4-用户数字id
    head.Uid = 0
    -- 4-用户FD
    head.Token = 0
    -- 4-回调索引,rpc用
    head.CallbackId = 0
    -- 8-实体对象id,如uid deskId
    head.Key = 0
    -- 8-消息发送时间,精确到毫秒
    head.SendTime = 0
    -- 4-功能编号,应用层用于识别功能
    head.FuncId = 0
    -- 4-消息内容长度
    head.Size = 0
    -- 2预留位置
    head.Free2 = 0
    -- 1-校验位
    head.Check = 0

    function head.encode()
        local bytebuff = ByteBuffer.Get()

        bytebuff:WriteByte(HeadPad) --1
        bytebuff:WriteByte(head.TM) --1
        bytebuff:WriteByte(head.RT) --1
        bytebuff:WriteByte(head.Free1) --1
        bytebuff:WriteInt64(head.Id) --8
        bytebuff:WriteInt32(head.Uid) --4
        bytebuff:WriteInt32(head.Token) --4
        bytebuff:WriteInt32(head.CallbackId) --4
        bytebuff:WriteInt64(head.Key) --8
        bytebuff:WriteInt64(head.SendTime) --8
        bytebuff:WriteInt32(head.FuncId) --4
        bytebuff:WriteInt32(head.Size) --4
        bytebuff:WriteInt16(head.Free2) --2

        local _check = head.TM + head.RT + head.Free1 + head.Id +
        head.Uid + head.Token + head.CallbackId +
        head.Key + head.SendTime + head.FuncId + head.Size + head.Free2
        head.Check = bit64:_and(_check, 0xFF);

        bytebuff:WriteByte(head.Check) --1
        bytebuff:WriteByte(EndPad) --1

        return bytebuff
    end

    function head.decode(bytebuff)
        bytebuff:ReadByte() --head.HeadPad
        head.TM = bytebuff:ReadByte()
        head.RT = bytebuff:ReadByte()
        head.Free1 = bytebuff:ReadByte()
        head.Id = bytebuff:ReadInt64()
        head.Uid = bytebuff:ReadInt32()
        head.Token = bytebuff:ReadInt32()
        head.CallbackId = bytebuff:ReadInt32()
        head.Key = bytebuff:ReadInt64()
        head.SendTime = bytebuff:ReadInt64()
        head.FuncId = bytebuff:ReadInt32()
        head.Size = bytebuff:ReadInt32()
        head.Free2 = bytebuff:ReadInt16()
        head.Check = bytebuff:ReadByte()
        bytebuff:ReadByte() --head.EndPad
    end

    return head
end

function GNet.Init()
    GEvent:on(NetEvents.OnSocketConnected, this.OnSocketConnected, "");
    GEvent:on(NetEvents.OnSocketTimeout, this.OnSocketTimeout, "");
    GEvent:on(NetEvents.OnSocketError, this.OnSocketError, "");
    GEvent:on(NetEvents.OnSocketClose, this.OnSocketClose, "");
    GEvent:on(NetEvents.OnSocketData, this.OnSocketData, "");
end

---连接服务器
---@param url string        服务器地址
---@param timeout integer   超时时间
---@param cb any            成功回调
function GNet.Connect(url, timeout, cb)
    ws.Connect(url, timeout)
    connectedHandler = cb
end

function GNet.SetMaxTime(t)
    maxTime = t
end

function GNet.GetMaxTime()
    return maxTime
end

---设置uid
---@param uid integer
function GNet.SetHeadUid(uid)
    msgHeadUid = uid
end

---设置token
---@param token integer
function GNet.SetHeadToken(token)
    msgHeadToken = token
end

function GNet.SetKeepalive(time, cb)
    local now = AppGlobal.GetServerTime()
    time = time or 15

    heartTime = time;
    maxTime = time * 1000

    local heartbeat = CreateHeartbeatSend(time)

    this.Send(0, 0, heartbeat, cb, 3, false)
end

function GNet.getMsgAutoID()
    msgAutoID = msgAutoID + 1
    return msgAutoID
end

-- 设置大厅消息的解码对象
function this.setHallDecoder(_decoder)
	this.hallDecoder = _decoder;
end

-- 设置游戏消息的解码对象
function this.setGameDecoder(_decoder)
	this.gameDecoder = _decoder
end

local rtFilter = {['112003'] = true, ["3011"] = true}
function GNet.getMsgRT(head)
    local ret = 1

    if (head.TM == 2) then
        ret = 0
    end

    if (rtFilter[tostring(head.FuncId)]) then
        ret = 0
    end
    return ret
end

---发送消息
---@param key integer           实体对象id
---@param gameId integer        游戏id 大厅消息填0
---@param msg table             C2S消息结构体
---@param cb function           回调消息
---@param timeout integer|nil   超时时间
---@param cached boolean|nil    是否缓存(仅缓存有回调的消息)
function GNet.Send(key, gameId, msg, cb, timeout, cached)
    local callid = cb ~= nil and cbHelper:saveCallback(nil, cb) or 0
    local funcid = gameId * 1000 + msg.GetFuncID()

    if cached then
        msgCache[callid] = {key, gameId, msg, cb, timeout, cached}
    end

    if AppGlobal.loginState == false and funcid ~= 100001 and funcid ~= 100002 then
        logError('GNet.Send(连接未登陆):', funcid)
        return 
    end

    local head = CreateMessageHead()
    head.TM = msg.GetTM()
    head.Id = this.getMsgAutoID()
    head.Uid = msgHeadUid;
    head.Token = msgHeadToken;
    head.CallbackId = callid
    head.Key = key;
    head.SendTime = AppGlobal.GetServerTime()
    head.FuncId = funcid
    head.RT = this.getMsgRT(head)

    -- 转ByteBuffer
    local mbuff = msg.Encode()
    head.Size = mbuff.Length
    mbuff.Position = 0

    log('[GNet] Send() - head:', table_tostring(head))

    -- 转ByteBuffer
    local hbuff = head.encode()
    -- 转字节流
    local mbytes = mbuff:ReadBytes(mbuff.Length)
    -- 加密后字节流
    local _mbytes = this.DecodeBody(head.Check, mbytes)

    hbuff:WriteBuffer(_mbytes)
    mbuff:Release()

    if head.FuncId ~= 6 and head.FuncId ~= 113007 then
        log('send msg time =', tostring(os.time()),'funcid =',head.FuncId, 'callid =',callid, 'msg =', table_tostring(msg))
    end

    -- TODO 请求类型的消息,添加异步5秒超时检测
    -- if head.CallbackId ~= 0 and not cached then

    -- end
    ws.Send(hbuff)
end

function GNet.CheckHeartbeat()
    if AppGlobal.loginState and AppGlobal.netState then
        local hbTime = 0
        if AppGlobal.deskID > 0 or AppGlobal.matchID > 0 then
            hbTime = 6
        else
            hbTime = 15
        end

        maxTime = hbTime * 1000

        local heartbeat = CreateHeartbeatSend(hbTime)
        this.Send(0, 0, heartbeat, function (ret)
            -- log('心跳消息 ====  ', json.encode(ret))
            if ret.serverTime > 0 then
                AppGlobal.SetServerTime(ret.serverTime * 1000)
            end
        end, 3, false)
    end
end

---消息加密解密
---@param check integer     秘钥
---@param bytes any         字节数组
---@param offset integer    偏移字节
---@return any
function GNet.DecodeBody(check, bytes, offset)
    offset = offset or 0
    log('[GNet] DecodeBody() ', check , bytes.Length, offset);

    for i = offset, bytes.Length - 1 do
        bytes[i] = bit:_xor(bytes[i], check)
    end

    return bytes
end

function GNet.Close()
    ws.Close()
end

function GNet.OnSocketConnected()
    log('[GNet] OnSocketConnected() - 连接成功')
    lastTime = os.time()
    AppGlobal.netState = true
    TimeTick.Add("GNet_CheckHeartbeat", this.CheckHeartbeat, 3, 0)

    connectedHandler();
end

function GNet.OnSocketTimeout()
    log('[GNet] OnSocketTimeout() - ')

end

function GNet.OnSocketError(bytebuf)
	-- 读取错误信息
	local errmsg = bytebuf:ReadString();
	bytebuf:Release()

    log('[GNet] OnSocketError() - errmsg: ', errmsg)

	GEvent:emit({ name = NetEvents.OnSocketError, errmsg = errmsg});
end

function GNet.OnSocketClose(bytebuf)
	-- 读取错误码
	local code = bytebuf:ReadInt16()
	bytebuf:Release()

    log('[GNet] OnSocketClose() - code: ', code)
    AppGlobal.netState = false

    GEvent:emit({ name = NetEvents.OnSocketClose, code = code});
end

function GNet.OnSocketData(bytebuff)
    log('[GNet] OnSocketData() - bytebuff: ', bytebuff, bytebuff.Length)

    lastTime = os.time()

    local head = CreateMessageHead()
    local headOffset = 52;

    head.decode(bytebuff)

    log('[GNet] OnSocketData() - head:', table_tostring(head))

    local funcid = head.FuncId
    local decoder = this.hallDecoder;

    -- 转字节流
    local mbytes = bytebuff:ReadBytes(head.Size)
    this.DecodeBody(head.Check, mbytes, 0)

    local _buffer = ByteBuffer.Get()
    _buffer:WriteBytes(mbytes, true)

    -- 1 大厅消息 2 游戏消息
    local msgtype = 1

    if funcid > 1000000 then
        --- 游戏消息
        head.Key = _buffer:ReadInt64()
        headOffset = headOffset + 8
        head.Size = head.Size - 8
        decoder = this.gameDecoder  --TODO 没有实现
        funcid = funcid % 1000
        msgtype = 2
        if AppGlobal.deskID ~= 0 and head.Key ~= AppGlobal.deskID and funcid ~= 77 then
            logError("非当前牌桌消息  head.Key = ", head.Key, " AppGlobal.deskID = ", AppGlobal.deskID, " funcid = ", funcid);
            return
        end
    end

	local ret = nil
	if head.Size > 0 then
		if msgtype == 1 then
			-- 指针去头域
			bytebuff.Position = headOffset

			if funcid == 6 then
				-- 二进制消息单独处理 如心跳消息
				local heartbeat = CreateHeartbeatRecv()
				ret = heartbeat.Decode(_buffer)
			else
				local jsonstr = _buffer:ReadString(head.Size)
				_buffer:Release()
				log('GNet.OnSocketData() - jsonstr:', jsonstr)
				ret = json.decode(jsonstr)
			end
		else
			-- TODO 处理游戏消息
			if decoder then
				ret = decoder.DecodeMsg(funcid, _buffer)
			end
		end
	end

	-- 解析失败 生成一个错误消息
	if ret == nil then
		ret = {['__retErr'] = 2}
	end 

	local callid = head.CallbackId
	if funcid ~= 6 and head.FuncId ~= 113007 then
		log('recv msg: funcid = ', funcid, ' callid = ', callid, ' time = ', AppGlobal.GetServerTime(), ' msg = ', table_tostring(ret))
	end

	-- 清除缓存
	if msgCache[callid] then
		msgCache[callid] = nil
	end

	if callid ~= 0 then
		cbHelper:dealCallback(callid, ret)
	else
		if msgtype == 1 then
			GEvent:emit({ name = NetEvents.on_deal_hallmsg, funcid = funcid, ret = ret});
		else
			GEvent:emit({ name = NetEvents.on_deal_gamemsg, funcid = funcid, ret = ret});
		end
	end

	--释放缓冲
	bytebuff:Release()

 
end

return GNet