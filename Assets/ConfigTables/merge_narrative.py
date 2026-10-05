#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Narrative Excel 合并脚本
将多个 Narrative_*.xlsx 合并为统一的 Narrative.xlsx
将多个 Text_*.xlsx 合并为统一的 Text.xlsx
"""

import os
import sys
import glob
import json
import shutil
from datetime import datetime
from collections import defaultdict

try:
    import openpyxl
    from openpyxl import Workbook, load_workbook
except ImportError:
    print("ERROR: 需要安装 openpyxl: pip install openpyxl")
    sys.exit(1)

DATA_DIR = os.path.join(os.path.dirname(__file__), "Data")
LOG_FILE = os.path.join(os.path.dirname(__file__), "merge_log.json")

# Luban 表头行数（前4行是表头）
HEADER_ROWS = 4

# Narrative 的所有工作表
NARRATIVE_SHEETS = [
    "Object", "Bar", "Trigger", "Event", "EventDate",
    "Result", "Condition", "Template", "SetupParams", "Choice"
]

# Text 的工作表
TEXT_SHEETS = ["Text"]


def load_workbook_safe(path):
    """安全加载 Excel，处理 .git 忽略等问题"""
    if not os.path.exists(path):
        return None
    try:
        return load_workbook(path, data_only=False)
    except Exception as e:
        print(f"WARNING: 无法加载 {path}: {e}")
        return None


def get_header_from_sheet(ws):
    """提取表头（前4行）"""
    header = []
    for row in ws.iter_rows(min_row=1, max_row=HEADER_ROWS, values_only=False):
        header.append([cell.value for cell in row])
    return header


def get_data_rows(ws):
    """提取数据行（从第5行开始）"""
    rows = []
    for row in ws.iter_rows(min_row=HEADER_ROWS + 1, values_only=False):
        values = [cell.value for cell in row]
        # 跳过全空行
        if any(v is not None and str(v).strip() != "" for v in values):
            rows.append(values)
    return rows


def get_primary_key_col_index(header):
    """
    根据表头推断主键列索引
    Luban 表头第2行是 ##type，但通常第1行 ##var 的第1个非空列是主键
    """
    if len(header) < 1:
        return 0
    var_row = header[0]
    for i, val in enumerate(var_row):
        if val is not None and str(val).strip() != "":
            return i
    return 0


def merge_sheets(source_files, target_wb, sheet_name, log_context):
    """
    合并多个源文件的同一个 sheet 到目标 workbook
    返回 (合并行数, 冲突列表)
    """
    conflicts = []
    all_rows = []
    seen_keys = {}  # key -> (来源文件, 行号)
    header = None

    for src_path in source_files:
        wb = load_workbook_safe(src_path)
        if wb is None:
            continue

        if sheet_name not in wb.sheetnames:
            continue

        ws = wb[sheet_name]
        current_header = get_header_from_sheet(ws)
        current_rows = get_data_rows(ws)

        if not current_rows:
            continue

        # 记录表头（用第一个非空文件的表头）
        if header is None and current_header:
            header = current_header

        # 推断主键列
        pk_col = get_primary_key_col_index(current_header)

        for row_idx, row in enumerate(current_rows, start=HEADER_ROWS + 1):
            pk_val = row[pk_col] if pk_col < len(row) else None
            pk_str = str(pk_val).strip() if pk_val is not None else ""

            # 检查主键冲突
            if pk_str and pk_str != "None":
                if pk_str in seen_keys:
                    prev_file, prev_row = seen_keys[pk_str]
                    conflicts.append({
                        "sheet": sheet_name,
                        "key": pk_str,
                        "file_a": prev_file,
                        "row_a": prev_row,
                        "file_b": os.path.basename(src_path),
                        "row_b": row_idx
                    })
                else:
                    seen_keys[pk_str] = (os.path.basename(src_path), row_idx)

            all_rows.append(row)

    # 写入目标 workbook
    if sheet_name in target_wb.sheetnames:
        del target_wb[sheet_name]

    ws = target_wb.create_sheet(title=sheet_name)

    # 写入表头
    if header:
        for r_idx, row in enumerate(header, start=1):
            for c_idx, val in enumerate(row, start=1):
                ws.cell(row=r_idx, column=c_idx, value=val)

    # 写入数据
    for r_idx, row in enumerate(all_rows, start=HEADER_ROWS + 1):
        for c_idx, val in enumerate(row, start=1):
            ws.cell(row=r_idx, column=c_idx, value=val)

    return len(all_rows), conflicts


def merge_narrative():
    """合并所有 Narrative_*.xlsx"""
    pattern = os.path.join(DATA_DIR, "Narrative_*.xlsx")
    source_files = sorted(glob.glob(pattern))

    # 必须包含 Core
    core_path = os.path.join(DATA_DIR, "Narrative_Core.xlsx")
    if core_path not in source_files:
        print(f"WARNING: 缺少核心文件 {core_path}")
        print("       将仅合并现有的 Narrative_*.xlsx 文件")
        # 如果没有 Core，但存在其他 Narrative_*.xlsx，仍然继续
        if not source_files:
            print("ERROR: 未找到任何 Narrative_*.xlsx 文件")
            return False, 0
    else:
        # 确保 Core 排在最前
        source_files.remove(core_path)
        source_files.insert(0, core_path)

    print(f"合并 Narrative，源文件: {[os.path.basename(p) for p in source_files]}")

    # 创建新的目标 workbook
    target_wb = Workbook()
    # 删除默认 sheet
    if "Sheet" in target_wb.sheetnames:
        del target_wb["Sheet"]

    all_conflicts = []
    total_rows = 0

    for sheet_name in NARRATIVE_SHEETS:
        rows, conflicts = merge_sheets(source_files, target_wb, sheet_name, None)
        total_rows += rows
        all_conflicts.extend(conflicts)
        print(f"  [{sheet_name}] 合并 {rows} 行")

    # 保存
    output_path = os.path.join(DATA_DIR, "Narrative.xlsx")
    # 备份旧文件
    if os.path.exists(output_path):
        backup_path = output_path.replace(".xlsx", f"_backup_{datetime.now().strftime('%m%d_%H%M')}.xlsx")
        shutil.copy2(output_path, backup_path)
        print(f"  已备份旧文件到: {os.path.basename(backup_path)}")

    target_wb.save(output_path)
    print(f"  已保存: {output_path}")

    return all_conflicts, total_rows


def merge_text():
    """合并所有 Text_*.xlsx"""
    pattern = os.path.join(DATA_DIR, "Text_*.xlsx")
    source_files = sorted(glob.glob(pattern))

    if not source_files:
        print("未找到 Text_*.xlsx，跳过文本合并")
        return [], 0

    print(f"合并 Text，源文件: {[os.path.basename(p) for p in source_files]}")

    target_wb = Workbook()
    if "Sheet" in target_wb.sheetnames:
        del target_wb["Sheet"]

    all_conflicts = []
    total_rows = 0

    for sheet_name in TEXT_SHEETS:
        rows, conflicts = merge_sheets(source_files, target_wb, sheet_name, None)
        total_rows += rows
        all_conflicts.extend(conflicts)
        print(f"  [{sheet_name}] 合并 {rows} 行")

    output_path = os.path.join(DATA_DIR, "Text.xlsx")
    if os.path.exists(output_path):
        backup_path = output_path.replace(".xlsx", f"_backup_{datetime.now().strftime('%m%d_%H%M')}.xlsx")
        shutil.copy2(output_path, backup_path)

    target_wb.save(output_path)
    print(f"  已保存: {output_path}")

    return all_conflicts, total_rows


def write_log(narrative_conflicts, narrative_rows, text_conflicts, text_rows):
    """记录合并日志"""
    log = {
        "timestamp": datetime.now().isoformat(),
        "narrative": {
            "rows": narrative_rows,
            "conflicts": narrative_conflicts
        },
        "text": {
            "rows": text_rows,
            "conflicts": text_conflicts
        }
    }
    with open(LOG_FILE, "w", encoding="utf-8") as f:
        json.dump(log, f, ensure_ascii=False, indent=2)


def main():
    print("=" * 60)
    print("Narrative Excel 合并工具")
    print("=" * 60)

    # 检查环境
    if not os.path.exists(DATA_DIR):
        print(f"ERROR: 数据目录不存在: {DATA_DIR}")
        sys.exit(1)

    # 合并 Narrative
    nar_conflicts, nar_rows = merge_narrative()

    # 合并 Text
    txt_conflicts, txt_rows = merge_text()

    # 写日志
    write_log(nar_conflicts, nar_rows, txt_conflicts, txt_rows)

    # 报告冲突
    all_conflicts = nar_conflicts + txt_conflicts

    print("\n" + "=" * 60)
    if all_conflicts:
        print(f"ERROR: 发现 {len(all_conflicts)} 个主键冲突!")
        for c in all_conflicts:
            print(f"  [{c['sheet']}] 键 '{c['key']}' 冲突:")
            print(f"    - {c['file_a']} 第{c['row_a']}行")
            print(f"    - {c['file_b']} 第{c['row_b']}行")
        print("\n请解决冲突后再运行 Luban 生成!")
        sys.exit(1)
    else:
        print(f"合并完成: Narrative {nar_rows} 行, Text {txt_rows} 行")
        print("无冲突，可以继续运行 Luban 生成")

    print("=" * 60)


if __name__ == "__main__":
    main()
