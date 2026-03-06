<%*
let url = 'https://www.tianqi.com/binhuqu/';
let weather = '无锡 天气获取失败 Weather fetch failed';
try {
  let res = await request({url: url, method: "GET"});
  res = res.replace(/\s/g,'');
  let r = /<ddclass="weather">[\s\S]*?<\/dd>/g;
  let data = r.exec(res)[0];
  r = /<span><b>(.*?)<\/b>(.*?)<\/span>/g;
  data = r.exec(data);
  weather = '无锡 ' + data[1] + ' ' + data[2];
} catch(e) {}
-%>
---
type: Work-Daily
role: Junior
tags:
  - work/junior
  - review/daily
date: <% tp.file.creation_date("YYYY-MM-DD-dddd") %>
weather: <% weather %>
project: 
sprint: 
jira_board: 
performance_score: # 1-5分 (Self-Assessment)
---

# 🌱 程序员成长日志 | Junior Dev Daily Log

> [!abstract] **管理者视角 | Manager's View**
> **今日产出 (Outputs):**
> - 
> **遇到的困难 (Blockers):**
> - 
> **今日学习进度 (Learning Progress):**
> - 
> **绩效自评 (Self-Score):** ⭐⭐⭐⭐⭐

---

## � 高价值资产积累 | Valuable Learning Assets (核心)
*不要只记录“做了什么”，要记录“学到了什么”*

### 1️⃣ � Bug 知识库 | Bug Knowledge Base
> *记录每一个报错和Bug，这是最宝贵的财富*
- **Error/Bug**: (e.g. VisionPro LoaderLock)
- **Environment (环境)**: WinForms + VisionPro
- **Root Cause (原因)**: OCX在LoaderLock中调用托管代码
- **Solution (解决)**: 禁用 LoaderLock 调试助手
- **Future Keywords (未来搜索词)**: TargetInvocationException, LoaderLock

### 2️⃣ 🛠️ 工具与命令记录 | Tools & Commands
> *记录新学到的工具和命令，方便以后直接复制*
- **Tool**: aravis
- **Usage (用途)**: 虚拟GigE相机
- **Command (命令)**: 
    ```bash
    aravis-fake-gv-camera -i 192.168.0.70
    ```

### 3️⃣ 🤖 AI 辅助学习 | AI Learning Prompts
> *记录你如何向AI提问来解决问题*
- **Task**: 解释多线程死锁
- **Prompt Used**: 
    > 
- **Key Takeaway**: 

---

## 📚 每日学习与成长 | Learning & Growth

### 📖 今日核心概念 (Key Concepts)
1. **Concept 1**: 
    - *理解 (Understanding)*: 
    - *应用场景 (Use Case)*: 
2. **Concept 2**: 

### 👨‍🏫 导师指导 (Mentorship)
- **Mentor**: 
    - *建议 (Advice)*: 
    - *待改进 (To Improve)*: 

---

## 🛠️ 任务与实践 | Tasks & Practice

### ✅ 任务清单 (Task List)
- [ ] **Task 1**: 
    - *Status*: Pending / Done
    - *难度 (Difficulty)*: Easy / Medium / Hard
- [ ] **Task 2**: 

### 💻 代码实践 (Coding Practice)
```csharp
// 记录今日写的一段觉得不错的代码，或者练习题
```

---

## 📝 总结与反思 | Reflection
- **今日最大的收获**: 
- **明日需要加强的点**: 
- **心情/状态**: 😊 / 😐 / 😫

---

## 🔄 日终检查 | End-of-Day Checklist
- [ ] 代码已提交且无报错 (Code Committed & No Errors)
- [ ] **核心资产已记录 (Bug/Tool/Prompt)**
- [ ] 学习笔记已整理 (Notes Organized)
- [ ] 向导师/组长汇报进度 (Reported Progress)

## 📊 今日笔记 | Daily Notes
```dataview
list
where file.cday = date(<% tp.file.creation_date("YYYY-MM-DD") %>)
sort file.ctime asc
```
