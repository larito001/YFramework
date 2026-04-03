# gtp/writer/ProtoHelper.py

# 配置类型 -> Proto 类型
TYPE_MAP: dict[str, str] = {
    "uint":              "uint32",
    "int":               "int32",
    "bool":              "int32",
    "string":            "string",
    "float":             "float",
    "vec2.int":          "Vector2Int",
    "vec2.float":        "Vector2Float",
    "vec3.int":          "Vector3Int",
    "vec3.float":        "Vector3Float",
    "array.uint":        "uint32",
    "array.int":         "int32",
    "array.float":       "float",
    "array.string":      "string",
    "array.bool":        "int32",
    "array.vec2.int":    "Vector2Int",
    "array.vec2.float":  "Vector2Float",
    "array.vec3.int":    "Vector3Int",
    "array.vec3.float":  "Vector3Float",
    "map.string.int":    "KeyValuePairStrInt",
    "map.string.float":  "KeyValuePairStrFloat",
}


def getProtoType(typeStr: str) -> str:
    """返回 proto 字段类型"""
    return TYPE_MAP.get(typeStr, typeStr)


def getProtoRule(typeStr: str) -> str:
    """返回 proto 字段规则：array/map 用 repeated，其余无前缀"""
    if "array" in typeStr or "map" in typeStr:
        return "repeated"
    return ""
