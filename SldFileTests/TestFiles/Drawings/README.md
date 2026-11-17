# 工程图测试样本

D8001507933/ 为现有工程图样本。MacroBenchmark/ 为 2026-11-09 从本机 SOLIDWORKS 安装目录 C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\sldBenchmarking\Macro 递归复制的 4 份工程图，共 18,894,693 字节；文件名、子目录和原始字节均保留，逐文件 SHA-256 与源文件一致。Drawings 下不保留零件或装配体文件。

导入工程图：carrier.slddrw、cavity.slddrw、punch_holder.slddrw、Mold/bezel moldbase.slddrw。外部模型均不包含，测试应使用工程图自身保存的预览，不依赖零件或装配体解析。源目录中排除的 7 份装配体见 [manifest.json](MacroBenchmark/manifest.json)。宏、焊件轮廓及其他扩展名未导入。

[manifest.json](MacroBenchmark/manifest.json) 记录来源、相对路径、大小及 SHA-256。[MFC_DECODE_GAPS.md](MFC_DECODE_GAPS.md) 列出当前工程图预览解码缺口，[mfc-decode-inventory.json](mfc-decode-inventory.json) 保存流和类声明的原始盘点结果。

测试项目已有 TestFiles 递归复制配置，无需为新增样本修改项目文件。此次只进行文件复制、流扫描和 DisplayLists 字节回写校验，没有修改或通过 SolidWorks 重新保存样本，也没有验证新样本的完整原生渲染。
