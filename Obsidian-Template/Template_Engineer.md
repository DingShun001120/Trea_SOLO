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
role: Engineer
tags:
  - work/engineer
  - review/daily
date: <% tp.file.creation_date("YYYY-MM-DD-dddd") %>
weather: <% weather %>
project: 
sprint: 
jira_board: 
performance_score: # 1-5分 (Self-Assessment)
---

# 🛡️ 工程师工作日志 | Engineer Daily Log

> [!abstract] **管理者视角 | Manager's View**
> **今日产出 (Key Outputs):**
> - 
> **阻碍 (Blockers):**
> - 
> **绩效自评 (Self-Score):** ⭐⭐⭐⭐⭐

---

## 📅 计划与进度 | Plan & Progress

### 🚀 冲刺任务 (Sprint Tasks)
- [ ] **Task 1**: 
    - *Status*: In Progress / Done
    - *PR/Commit*: 
- [ ] **Task 2**: 
    - *Status*: Pending

---

## 💎 核心资产积累 | Valuable Assets Log (重点)
*拒绝流水账，只记录真正有价值的内容*

### 1️⃣ 🐞 Bug 知识库 | Bug Knowledge Base
> *遇到 LoaderLock, TargetInvocationException 等疑难杂症必填*
- **Bug/Issue**: 
- **Environment (环境)**: 
- **Root Cause (原因)**: 
- **Solution (解决)**: 
- **Keywords (关键词)**: #Tag1 #Tag2

### 2️⃣ ⚡ 性能与系统优化 | Performance & Optimization
> *记录系统卡顿、内存泄漏等问题的优化过程*
- **Symptom (现象)**: 
- **Diagnosis (诊断)**: 
- **Optimization (优化)**: 
    - *Before*: 
    - *After*: 

### 3️⃣ 🛠️ 工具百宝箱 | Toolbox
> *记录新学到的命令行工具、插件或脚本*
- **Tool Name**: 
- **Usage (用途)**: 
- **Command/Snippet**:
    ```bash
    # 粘贴你的命令
    ```

### 4️⃣ 🤖 AI 交互资产 | AI Prompt Assets
> *记录高效的 Prompt，形成个人的 Prompt Library*
- **Goal (目标)**: 
- **Prompt Used**: 
    > 
- **Result/Insight**: 

---

## 💻 技术攻坚 | Technical Deep Dive
*记录今日遇到的架构思考或技术选型*

### ⚖️ 技术选型记录 | Tech Stack Selection (Optional)
- **Topic**: (e.g. MQTT vs RabbitMQ)
- **Decision**: 
- **Reasons**: 
    1. 
    2. 

### 代码片段 (Code Snippet)
```csharp
// 关键代码或重构逻辑
```

---

## 📝 协作与会议 | Collaboration
- **Code Review**: 
    - *Reviewer*: 
    - *Feedback*: 
- **会议 (Meetings)**:
    - *Topic*: 
    - *Action Items*: 

---

## 🔄 日终检查 | End-of-Day Checklist
- [ ] 代码已提交 (Code Committed)
- [ ] 任务状态已更新 (Jira/Board Updated)
- [ ] **核心资产已记录 (Bug/Tool/Prompt)**
- [ ] 明日计划已梳理 (Tomorrow Planned)

## 📊 今日数据 | Daily Stats
```dataview
list
where file.cday = date(<% tp.file.creation_date("YYYY-MM-DD") %>)
sort file.ctime asc
```
