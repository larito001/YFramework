# gtp/common/ConfigData.py

from dataclasses import dataclass, field

ARRAY_SEP = "|"
MAP_KEY_VAL_SEP = "~"
BASE_DATA_TYPE = {"int", "uint", "string", "float", "bool"}
COMPLEX_TYPE = {"map", "array", "vec2", "vec3"}


@dataclass
class ConfigData:
    id: str = ""
    name: str = ""
    colID: list[str] = field(default_factory=list)
    colName: list[str] = field(default_factory=list)
    colType: list[str] = field(default_factory=list)
    colTarget: list[str] = field(default_factory=list)
    colDefault: list = field(default_factory=list)
    dataIndex: list[list[str]] = field(default_factory=list)
    dataIndexC: list[list[str]] = field(default_factory=list)
    data: list[list] = field(default_factory=list)
    ext: list[str] = field(default_factory=list)


def isValidType(typeStr: str) -> bool:
    """校验类型字符串是否合法"""
    if typeStr in BASE_DATA_TYPE:
        return True
    items = typeStr.split(".")
    if len(items) <= 1:
        return False
    match items[0]:
        case "array":
            return isValidType(".".join(items[1:]))
        case "vec2" | "vec3":
            return len(items) == 2 and items[1] in BASE_DATA_TYPE
        case "map":
            return len(items) == 3 and items[1] in BASE_DATA_TYPE and items[2] in BASE_DATA_TYPE
    return False


def getDefaultValue(typeStr: str):
    """获取类型的默认值"""
    match typeStr:
        case "int":
            return -1
        case "uint":
            return 0
        case "string":
            return ""
        case "float":
            return 0
        case "bool":
            return 0
    prefix = typeStr.split(".")[0]
    match prefix:
        case "array":
            return []
        case "vec2":
            return [0, 0]
        case "vec3":
            return [0, 0, 0]
        case "map":
            return {}
    return None


def getPythonValue(val, typeStr: str, defaultVal):
    """将单元格值转为 Python 对象"""
    if val is None or (isinstance(val, str) and len(val) == 0):
        return defaultVal if defaultVal is not None else getDefaultValue(typeStr)

    match typeStr:
        case "int" | "uint":
            return int(val)
        case "string":
            return str(val)
        case "float":
            return float(val)
        case "bool":
            s = str(val).lower()
            if s == "true":
                return 1
            if s == "false":
                return 0
            return int(val)

    items = typeStr.split(".", 1)
    prefix, sub = items[0], items[1] if len(items) > 1 else ""

    match prefix:
        case "array":
            vals = str(val).split(ARRAY_SEP)
            return [getPythonValue(v, sub, None) for v in vals]

        case "vec2":
            val = str(val).strip()
            if val == "0":
                return [0, 0]
            vals = val.split("~")
            r = [getPythonValue(v, sub, None) for v in vals]
            if len(r) != 2:
                raise ValueError(f"vec2 需要恰好2个分量, 实际得到 {len(r)}: {val}")
            return r

        case "vec3":
            val = str(val).strip()
            if val == "0":
                return [0, 0, 0]
            vals = val.split("~")
            r = [getPythonValue(v, sub, None) for v in vals]
            if len(r) != 3:
                raise ValueError(f"vec3 需要恰好3个分量, 实际得到 {len(r)}: {val}")
            return r

        case "map":
            val = str(val).strip()
            if val == "0":
                return {}
            typeStrs = typeStr.split(".")  # ["map", keyType, valType]
            vals = val.split(ARRAY_SEP)
            r = {}
            for pair in vals:
                if pair.strip():
                    kvs = pair.split("~")
                    if len(kvs) < 2:
                        raise ValueError(f"map 键值对格式错误, 缺少分隔符'~': {pair}")
                    r[kvs[0]] = getPythonValue(kvs[1], typeStrs[2], None)
            return r

    raise ValueError(f"未知类型: {typeStr}")


def splitConfigClient(data: ConfigData) -> ConfigData:
    """从完整 ConfigData 中提取仅客户端列，返回新的 ConfigData"""
    client = ConfigData(id=data.id, name=data.name, ext=list(data.ext))

    # 过滤列：仅保留 colTarget 含 "C" 的列
    clientColIndices: list[int] = []
    for i in range(len(data.colID)):
        if "C" in data.colTarget[i]:
            clientColIndices.append(i)
            client.colID.append(data.colID[i])
            client.colName.append(data.colName[i])
            client.colType.append(data.colType[i])
            client.colTarget.append(data.colTarget[i])
            client.colDefault.append(data.colDefault[i])

    # 过滤数据行
    for row in data.data:
        client.data.append([row[i] for i in clientColIndices])

    # 合并索引并去重：dataIndex 中客户端可用的 + dataIndexC
    seen: set[tuple[str, ...]] = set()
    for idx in data.dataIndex:
        if all(k in client.colID for k in idx):
            key = tuple(idx)
            if key not in seen:
                seen.add(key)
                client.dataIndex.append(idx)
    for idx in data.dataIndexC:
        key = tuple(idx)
        if key not in seen:
            seen.add(key)
            client.dataIndex.append(idx)

    return client
