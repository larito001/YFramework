# gtp/common/ConfigManager.py

import configparser


class ConfigManager:
    """从 .ini 文件读取路径配置"""

    def __init__(self, iniPath: str = "tools_config.ini"):
        self.config = configparser.ConfigParser()
        self.config.read(iniPath, encoding="utf-8")

    @property
    def config_dir(self) -> str:
        return self.config.get("path", "config_dir")

    @property
    def client_dir(self) -> str:
        return self.config.get("path", "client_dir")

    @property
    def proto_dir(self) -> str:
        return self.config.get("path", "proto_dir")
