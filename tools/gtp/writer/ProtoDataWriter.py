# gtp/writer/ProtoDataWriter.py

from gtp.common.ConfigData import ConfigData
from gtp.writer import ProtoHelper


def _fillItem(item, data: ConfigData, row: list, colCount: int, pb2_module, base_pb2_module) -> None:
    """填充一条 protobuf 消息的所有字段"""
    for colIdx in range(colCount):
        cellVal = row[colIdx]
        if cellVal is None:
            continue

        fieldName = data.colID[colIdx]
        fieldRule = ProtoHelper.getProtoRule(data.colType[colIdx])
        fieldType = ProtoHelper.getProtoType(data.colType[colIdx])

        if fieldRule == "repeated":
            if isinstance(cellVal, list):
                if fieldType in ("Vector2Int", "Vector2Float", "Vector3Int", "Vector3Float"):
                    for vecVal in cellVal:
                        vec = getattr(item, fieldName).add()
                        vec.x = vecVal[0]
                        vec.y = vecVal[1]
                        if fieldType in ("Vector3Int", "Vector3Float"):
                            vec.z = vecVal[2]
                else:
                    getattr(item, fieldName).extend(cellVal)
            elif isinstance(cellVal, dict):
                for k, v in cellVal.items():
                    kvp = getattr(item, fieldName).add()
                    kvp.k = k
                    kvp.v = v
        else:
            match fieldType:
                case "Vector2Int" | "Vector2Float":
                    field = getattr(item, fieldName)
                    field.x = cellVal[0]
                    field.y = cellVal[1]
                case "Vector3Int" | "Vector3Float":
                    field = getattr(item, fieldName)
                    field.x = cellVal[0]
                    field.y = cellVal[1]
                    field.z = cellVal[2]
                case _:
                    setattr(item, fieldName, cellVal)


def writeData(path: str, data: ConfigData, pb2_module, base_pb2_module=None) -> bool:
    """
    将 ConfigData 序列化为 .bytes 文件（纯 protobuf LIST 格式）。
    C# 端通过 Deserialize<XXX_LIST_TOOL_RESERVED> 直接反序列化整个文件。
    """
    className = data.id[0].upper() + data.id[1:]
    colCount = len(data.colID)
    rowCount = len(data.data)

    if colCount <= 0 or rowCount <= 0:
        return False

    ListClass = getattr(pb2_module, f"{className}_LIST_TOOL_RESERVED")
    listObj = ListClass()

    for rowIdx in range(rowCount):
        row = data.data[rowIdx]
        item = listObj.items.add()
        _fillItem(item, data, row, colCount, pb2_module, base_pb2_module)

    with open(path, 'wb') as f:
        f.write(listObj.SerializeToString())

    return True
