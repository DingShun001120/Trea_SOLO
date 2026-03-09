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
uid: "<% tp.file.creation_date('YYYYMMDDHHmm') %>"
aliases: []
标签:
  - work/junior
  - review/daily
  - "log/<% tp.date.now('YYYY') %>/<% tp.date.now('MM') %>"
角色: Junior
状态: Done
周次: "<% tp.date.now('YYYY-[W]ww') %>"
季度: "<% tp.date.now('YYYY-[Q]Q') %>"
天气: "<% tp.user.weather(tp) %>"
项目: []
sprint: ""
mentor: []
learning_hours: 2
performance_score: 5
cssclasses: []
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

## 💎 核心资产积累 | Core Assets Log (5维记录)
*通过记录这5类信息，加速从小白到大牛的进阶*

### 1️⃣ 🐞 问题追踪 | Problem Tracking
> *记录遇到的报错、异常或运行失败的原因*
- **Error/Bug**: (e.g. 数据库连接超时)
- **Symptom**: 
- **Fix**: 
- **Reference**: 

### 2️⃣ 📚 技术沉淀 | Technical Accumulation
> *记录今日学到的新概念、API 用法或代码技巧*
- **Concept**: (e.g. 依赖注入的生命周期)
- **Understanding**: 
- **Example**: 

### 3️⃣ 🏗️ 架构演进 | Architecture Evolution (Learning)
> *记录对现有系统架构的理解，或阅读源码的心得*
- **Module Read**: (e.g. 认证模块)
- **My Understanding**: 
    - *Flow*: 

### 4️⃣ ⚡ 效率优化 | Efficiency Optimization
> *记录新掌握的快捷键、命令行工具或调试技巧*
- **Tool/Shortcut**: (e.g. VS Code 多光标编辑)
- **Usage**: 
- **Time Saved**: 

### 5️⃣ 🤖 AI 协作记录 | AI Collaboration Log
> *记录利用 AI 学习新知识或解决报错的过程*
- **Goal**: 
- **Prompt**: 
    > 
- **What I Learned**: 

---

## 🛠️ 任务与实践 | Tasks & Practice

### ✅ 任务清单 (Task List)
- [ ] **Task 1**: 
    - *Status*: Pending / Done
- [ ] **Task 2**: 

### 💻 代码实践 (Coding Practice)
```csharp
// 记录今日写的一段觉得不错的代码，或者练习题
```

---

## 📝 总结与反思 | Reflection
- **今日最大的收获**: 
- **明日需要加强的点**: 

---

## 🔄 日终检查 | End-of-Day Checklist
- [ ] 代码已提交且无报错 (Code Committed)
- [ ] **核心5维资产已记录**
- [ ] 学习笔记已整理 (Notes Organized)

## 📊 今日笔记 | Daily Notes
```dataview
list
where file.cday = date(<% tp.file.creation_date("YYYY-MM-DD") %>)
sort file.ctime asc
```
