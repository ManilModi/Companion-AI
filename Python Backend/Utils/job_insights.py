# -*- coding: utf-8 -*-
import pandas as pd
import numpy as np
import plotly.express as px
import plotly.graph_objects as go
import plotly.io as pio
import json

# Load dataset
df = pd.read_csv("./Datasets/ai_job_market_insights.csv")

charts = {}

# 1. Average Salary by Job Title
fig1 = px.bar(
    df.groupby("Job_Title", as_index=False)["Salary_USD"].mean().sort_values(by="Salary_USD", ascending=False),
    x="Job_Title", y="Salary_USD",
    title="Average Salary by Job Title", text_auto=True
)
fig1.update_traces(textposition="outside")
fig1.update_layout(xaxis_tickangle=-45, showlegend=False)
charts["salary_by_job"] = pio.to_html(fig1, full_html=False, include_plotlyjs='cdn')

# 2. Jobs by Industry
fig2 = px.pie(df, names="Industry", title="Distribution of Jobs by Industry", hole=0.3)
fig2.update_traces(textinfo='percent+label')
charts["jobs_by_industry"] = pio.to_html(fig2, full_html=False, include_plotlyjs=False)

# 3. Salary Distribution
fig3 = px.histogram(df, x="Salary_USD", nbins=30, title="Salary Distribution", marginal="box")
fig3.update_traces(marker_color="indianred")
charts["salary_distribution"] = pio.to_html(fig3, full_html=False, include_plotlyjs=False)

# 4. Salary vs AI Adoption
fig4 = px.box(df, x="AI_Adoption_Level", y="Salary_USD", title="Salary vs AI Adoption Level by Company")
charts["salary_vs_ai"] = pio.to_html(fig4, full_html=False, include_plotlyjs=False)

# 5. Remote Friendly Jobs
fig5 = px.histogram(
    df, x="Industry", color="Remote_Friendly",
    title="Remote-Friendly Jobs by Industry", barmode="group", text_auto=True
)
fig5.update_layout(xaxis_tickangle=-45)
charts["remote_friendly"] = pio.to_html(fig5, full_html=False, include_plotlyjs=False)

# 6. Automation Risk
fig6 = px.histogram(
    df, x="Industry", color="Automation_Risk",
    title="Automation Risk factor for jobs", barmode="group", text_auto=True
)
fig6.update_layout(xaxis_tickangle=-45)
charts["automation_risk"] = pio.to_html(fig6, full_html=False, include_plotlyjs=False)

# 7. AI Adoption
fig7 = px.histogram(
    df, x="Industry", color="AI_Adoption_Level",
    title="Automation Adoption factor for jobs", barmode="group", text_auto=True
)
fig7.update_layout(xaxis_tickangle=-45)
charts["ai_adoption"] = pio.to_html(fig7, full_html=False, include_plotlyjs=False)

# 8. Job Growth Projections
fig8 = px.histogram(
    df, x="Job_Title", color="Job_Growth_Projection",
    title="Job Growth Projections", barmode="group", text_auto=True,
    color_discrete_map={"High": "#1f77b4", "Moderate": "#ff7f0e", "Low": "#2ca02c"}
)
fig8.update_layout(xaxis_tickangle=-45)
charts["job_growth"] = pio.to_html(fig8, full_html=False, include_plotlyjs=False)


stats = {
    "total_jobs": int(len(df)),
    "top_skill": df["Skills"].value_counts().idxmax() if "Skills" in df.columns else "N/A",
    "average_salary": round(df["Salary_USD"].mean(), 2)
}

# Skills breakdown
skills = {
    "labels": df["Skills"].value_counts().index.tolist()[:10] if "Skills" in df.columns else [],
    "data": df["Skills"].value_counts().values.tolist()[:10] if "Skills" in df.columns else []
}

# Salary range distribution
salary_range = {
    "labels": ["0-50k", "50k-100k", "100k-150k", "150k+"],
    "data": [
        int(len(df[df["Salary_USD"] <= 50000])),
        int(len(df[(df["Salary_USD"] > 50000) & (df["Salary_USD"] <= 100000)])),
        int(len(df[(df["Salary_USD"] > 100000) & (df["Salary_USD"] <= 150000)])),
        int(len(df[df["Salary_USD"] > 150000]))
    ]
}

# Locations breakdown
locations = {
    "labels": df["Location"].value_counts().index.tolist()[:10] if "Location" in df.columns else [],
    "data": df["Location"].value_counts().values.tolist()[:10] if "Location" in df.columns else []
}

# Trends
trends = {
    "labels": df["Year"].value_counts().sort_index().index.tolist() if "Year" in df.columns else [],
    "data": df["Year"].value_counts().sort_index().values.tolist() if "Year" in df.columns else []
}


output = {
    "charts": charts,
    "stats": stats,
    "skills": skills,
    "salary_range": salary_range,
    "locations": locations,
    "trends": trends
}


with open("charts.json", "w") as f:
    json.dump(output, f)
