<%*
const config = {
    location: "浙江省温州市", // 默认位置，若脚本获取失败则使用此值
    adcode: "", 
    weather: ""
};
const meta = await tp.user.getDiaryMeta(tp, config);
_%>
---
uid: "<% tp.file.creation_date('YYYYMMDDHHmm') %>"
tags:
  - Topic/DailyNote
  - Role/Engineer
title: <% tp.file.title %>
type:
  - Daily Note
date: <% tp.date.now("YYYY-MM-DD") %>
created_time: <% tp.file.creation_date("YYYY-MM-DD HH:mm:ss") %>
modify_time: <% tp.file.last_modified_date("YYYY-MM-DD HH:mm:ss") %>
lunar_calendar: <% meta.lunarDate %>
week: <% tp.file.creation_date("dddd") %>
location: <% meta.location %>
weather: <% meta.weather %>
working_minutes: <%*
let day = tp.file.creation_date("dddd");
if (day === "星期日") {
tR += "0";
} else {
tR += "540";
}
%>
health:
events:
aliases:
  - Daily Note - <% tp.date.now("YYYY-MM-DD") %>
---

# Daily Note - <% tp.date.now("YYYY-MM-DD") %>

## 🚀 任务推进 (Task Execution)
> *聚焦当下，优先处理阻塞性任务与当日计划*

### 🔥 冲刺待办 (Sprint Focus)
```tasks
(heading includes 本周) OR (due on or before <% tp.date.now("YYYY-MM-DD", 0) %>) OR (scheduled on or before <% tp.date.now("YYYY-MM-DD", 0) %>)
tags does not include #Topic/Ignore
not done
hide recurrence rule
hide created date
# 优先显示高优先级任务
group by priority
# 其次按截止日期排序
sort by due
```

### 📥 待办跟进 (Inbox & Follow-up)
```tasks
((no scheduled date) OR (scheduled after <% tp.date.now("YYYY-MM-DD", 0) %>)) AND ((no due date) OR (due after <% tp.date.now("YYYY-MM-DD", 0) %>))
tags regex does not match /(#Topic/Ignore|#Topic/Trigger)/
path regex matches /(Archive\/Work|Collect\/Projects)/
not done
group by filename
```

---

## 💻 工作记录 (Work Traceability)
> *可追溯的开发日志，记录关键变更与决策*

### 📝 开发日志 (Dev Log)
| 时间 | 项目/模块 | 关键动作 (Action) | 结果/产出 (Output) |
| :--- | :--- | :--- | :--- |
| <% tp.date.now("HH:mm") %> |  |  |  |

### 🐞 问题追踪 (Bug Tracking)
- **Issue**: 
    - *Context*: 
    - *Root Cause*: 
    - *Solution*: 

### 📅 会议摘要 (Meeting Notes)
- **主题**: 
    - *结论*: 
    - *TODO*: 

---

## 🧠 技术沉淀 (Technical Accumulation)
> *记录可复用的技术方案、代码片段或踩坑经验*

### 💡 今日新知 (Learnings)
- **Topic**: #Tech/
    - **Problem**: 
    - **Solution/Snippet**:
      ```code
      
      ```
    - **Reference**: 

---

## ✅ 完成情况 (Achievements)
> *今日已完成事项自动汇总*

```tasks
done on <% tp.date.now("YYYY-MM-DD") %>
hide task count
group by filename
```

---

## 🧘 生活与复盘 (Life & Review)

### 健康管理
- [ ] 👁️ 护眼 (20-20-20法则)
- [ ] 💧 饮水 (>1500ml)
- [ ] 🚶 久坐提醒 (每小时起身)

### 每日收尾
- [ ] 代码已提交 (Git Commit)
- [ ] 工时已填报 (Odoo)
- [ ] 明日计划已更新

### 💭 个人感悟
- 

```tasks
tags includes #Topic/Plans
not done
hide task count
short mode
heading includes 中长期计划
```
