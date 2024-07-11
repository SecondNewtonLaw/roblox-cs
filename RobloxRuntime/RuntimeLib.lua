local CS = {}
local assemblyGlobal = {}

local fempty = function() end

local split = string.split
if split == nil then
	split = function(inputString, separator)
		if sep == nil then
			sep = "%s"
		end
		local t = {}
		for str in string.gmatch(inputString, "([^"..sep.."]+)") do
			table.insert(t, str)
		end
		return t
	end
end

local function insertRuntimeEnv(f)
	local env
	if getfenv == nil then
		env = _ENV
	else
		env = getfenv(f)
	end

	local Console = {} do
		function Console.WriteLine(...)
			print(...)
		end
		Console.Write = Console.WriteLine
		Console.Read = fempty
		Console.ReadLine = fempty
		Console.Clear = fempty
	end
	local Math = {} do
		Math.PI = math.pi
		Math.Pow = math.pow
	end

	env.Console = Console
	env.Math = Math;
	return f
end

local CSNamespace = {} do
	CSNamespace.__index = CSNamespace

	function CSNamespace.new(name)
		local self = {}
		self.name = name
		self.members = {}
		self["$loadCallbacks"] = {}
		return setmetatable(self, CSNamespace)
	end

	function CSNamespace:__index(index)
		return self.members[index] or CSNamespace[index]
	end

	CSNamespace["$getMember"] = function(self, name)
		return self.members[name]
	end

	CSNamespace["$onLoaded"] = function(self, callback)
		table.insert(self["$loadCallbacks"], callback)
	end

	function CSNamespace:class(name, create)
		local class = insertRuntimeEnv(create)(self)
		self.members[name] = class
	end
	
	function CSNamespace:namespace(name, registerMembers)
		CS.namespace(name, registerMembers, self.members)
	end
end

function CS.namespace(name, registerMembers, location)
	if location == nil then
		location = assemblyGlobal
	end

	local namespaceDefinition = CSNamespace.new(name)
	insertRuntimeEnv(registerMembers)(namespaceDefinition)
	for _, callback in pairs(namespaceDefinition["$loadCallbacks"]) do
		callback()
	end

	location[name] = namespaceDefinition
	return namespaceDefinition
end

function CS.getAssemblyType(name)
	local env
	if getfenv == nil then
		env = _ENV
	else
		env = getfenv(f)
	end
	return assemblyGlobal[name] or env[name]
end

return CS