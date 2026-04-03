# publish_config.py

import os
import sys
import importlib
import configparser
from grpc_tools import protoc as grpc_protoc
from gtp.common.ConfigData import splitConfigClient
from gtp.reader.ExcelReader import readExcel
from gtp.writer import ProtoWriter, ProtoDataWriter, CSWriter


def loadConfig(iniPath: str = "tools_config.ini") -> dict[str, any]:
    """从指定 ini 文件加载路径配置"""
    config = configparser.ConfigParser()
    config.read(iniPath, encoding="utf-8")
    return {
        "config_dir":   config.get("path", "config_dir"),
        "client_dir":   config.get("path", "client_dir"),
        "proto_dir":    config.get("path", "proto_dir"),
        "cs_proto_dir": config.get("path", "cs_proto_dir"),
        "protoc":       os.path.abspath(config.get("path", "protoc")),
        "skip_files":   [s.strip() for s in config.get("publish", "skip_files", fallback="").split(",") if s.strip()],
    }


def compileProto(protoDir: str, outputDir: str, protocPath: str, lang: str = "python") -> None:
    """编译所有 .proto 文件，Python 用 grpc_tools 内置 protoc，C# 用外部 protoc"""
    os.makedirs(outputDir, exist_ok=True)
    outFlag = "--python_out=" if lang == "python" else "--csharp_out="
    for f in os.listdir(protoDir):
        if f.endswith(".proto"):
            if lang == "python":
                # 使用 grpc_tools 内置的 protoc（版本与 protobuf 库匹配）
                ret = grpc_protoc.main([
                    "protoc",
                    f"--proto_path={protoDir}",
                    f"{outFlag}{outputDir}",
                    os.path.join(protoDir, f),
                ])
                if ret != 0:
                    raise RuntimeError(f"protoc failed: {f} (exit code {ret})")
            else:
                # C# 编译仍用外部 protoc
                import subprocess
                result = subprocess.run(
                    [protocPath, f"--proto_path={protoDir}", f"{outFlag}{outputDir}", f],
                    capture_output=True,
                )
                if result.returncode != 0:
                    err = result.stderr.decode("utf-8", errors="replace").strip()
                    print(f"  Skip: {f} ({err})")


def publishFile(filePath: str, cfg: dict) -> bool:
    """发布单个 Excel 配置文件"""
    fileID = os.path.splitext(os.path.basename(filePath))[0]

    if os.path.basename(filePath) in cfg["skip_files"]:
        print(f"  Skip (in skip list): {fileID}")
        return False

    print(f"Publishing: {fileID}")

    # 1. 读取 Excel
    rawData = readExcel(filePath)
    if rawData is None:
        print(f"  Skip (no data): {fileID}")
        return False

    # 2. 仅保留客户端列
    clientData = splitConfigClient(rawData)
    if not clientData.colID or not clientData.dataIndex:
        print(f"  Skip (no client columns): {fileID}")
        return False

    className = fileID[0].upper() + fileID[1:]

    # 3. 生成 .proto
    protoPath = os.path.join(cfg["proto_dir"], f"{fileID}.proto")
    ProtoWriter.writeData(protoPath, clientData)
    print(f"  -> {protoPath}")

    # 4. 生成 .cs
    csPath = os.path.join(cfg["client_dir"], "ScriptGenerated", "Config", f"{className}Config.cs")
    os.makedirs(os.path.dirname(csPath), exist_ok=True)
    CSWriter.writeData(csPath, clientData)
    print(f"  -> {csPath}")

    return True


def publishAll(cfg: dict) -> None:
    """遍历目录发布所有 Excel 文件"""
    configDir = os.path.join(cfg["config_dir"], "3xlsx")
    count = 0

    for root, dirs, files in os.walk(configDir):
        # 跳过 _temp 目录
        dirs[:] = [d for d in dirs if d != "_temp"]

        for f in files:
            ext = os.path.splitext(f)[1].lower()
            if ext not in (".xls", ".xlsx", ".xlsm"):
                continue
            if "~" in f or "$" in f:
                continue  # 跳过临时文件

            filePath = os.path.join(root, f)
            if publishFile(filePath, cfg):
                count += 1

    print(f"\nDone. Published {count} files.")


def generateBytes(cfg: dict) -> None:
    """编译 proto 并生成 .bytes 数据"""
    protoDir = cfg["proto_dir"]
    pb2Dir = os.path.join(os.path.dirname(protoDir), "pb2")
    protocPath = cfg["protoc"]

    # 先生成 ConfigBaseType.proto
    baseProtoPath = os.path.join(protoDir, "ConfigBaseType.proto")
    ProtoWriter.writeBaseTypes(baseProtoPath)

    # 编译所有 .proto 为 pb2
    print("\nCompiling proto files...")
    compileProto(protoDir, pb2Dir, protocPath)

    # 将 pb2 目录加入 sys.path
    if pb2Dir not in sys.path:
        sys.path.insert(0, pb2Dir)

    # 导入基础类型模块
    base_pb2 = importlib.import_module("ConfigBaseType_pb2")

    # 为每个表生成 .bytes
    configDir = os.path.join(cfg["config_dir"], "3xlsx")
    bytesDir = os.path.join(cfg["client_dir"], "Resources", "Config", "Data")
    os.makedirs(bytesDir, exist_ok=True)

    for root, dirs, files in os.walk(configDir):
        dirs[:] = [d for d in dirs if d != "_temp"]
        for f in files:
            ext = os.path.splitext(f)[1].lower()
            if ext not in (".xls", ".xlsx", ".xlsm"):
                continue
            if "~" in f or "$" in f:
                continue

            filePath = os.path.join(root, f)
            fileID = os.path.splitext(f)[0]

            rawData = readExcel(filePath)
            if rawData is None:
                continue

            clientData = splitConfigClient(rawData)
            if not clientData.colID or not clientData.dataIndex:
                continue

            className = fileID[0].upper() + fileID[1:]
            pb2Name = f"{fileID}_pb2"

            try:
                pb2_module = importlib.import_module(pb2Name)
                bytesPath = os.path.join(bytesDir, f"{className}.bytes")
                ProtoDataWriter.writeData(bytesPath, clientData, pb2_module, base_pb2)
                print(f"  -> {bytesPath}")
            except Exception as e:
                print(f"  Error generating bytes for {fileID}: {e}")


if __name__ == "__main__":
    # 支持通过 --config 指定配置文件
    iniPath = "tools_config.ini"
    args = sys.argv[1:]
    if len(args) >= 2 and args[0] == "--config":
        iniPath = args[1]
        args = args[2:]

    cfg = loadConfig(iniPath)

    # 确保输出目录存在
    os.makedirs(cfg["proto_dir"], exist_ok=True)

    # 先生成 ConfigBaseType.proto
    baseProtoPath = os.path.join(cfg["proto_dir"], "ConfigBaseType.proto")
    ProtoWriter.writeBaseTypes(baseProtoPath)
    print(f"Generated: {baseProtoPath}")

    # 生成 ConfigBase.cs 基类
    csConfigDir = os.path.join(cfg["client_dir"], "ScriptGenerated", "Config")
    baseCsPath = CSWriter.writeBaseClass(csConfigDir)
    print(f"Generated: {baseCsPath}")

    if args:
        # 发布指定文件
        for path in args:
            publishFile(path, cfg)
    else:
        # 发布全部
        publishAll(cfg)

    # 编译 proto 并生成 .bytes
    generateBytes(cfg)

    # 编译 proto 为 C#
    csProtoDir = cfg["cs_proto_dir"]
    os.makedirs(csProtoDir, exist_ok=True)
    print("\nCompiling proto to csharp...")
    compileProto(cfg["proto_dir"], csProtoDir, cfg["protoc"], lang="csharp")
