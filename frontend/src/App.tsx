import React from "react";
import Layout from "./components/Layout";
import { Routes, Route, Navigate } from "react-router-dom";
import BillListPage from "./pages/bill/BillListPage";
import ExpenseEditPage from "./pages/expenses/ExpenseEditPage";
import { LocalizationProvider } from "@mui/x-date-pickers";
import { AdapterDayjs } from "@mui/x-date-pickers/AdapterDayjs";

function App() {
  return (
    <LocalizationProvider dateAdapter={AdapterDayjs}>
      <Layout>
        <Routes>
          <Route path="/" element={
            <>
              <h2>欢迎使用账单智能检测系统！</h2>
              <p>请选择左侧功能菜单进行操作。</p>
            </>
          } />
          <Route path="/bills" element={<BillListPage />} />
          <Route path="/add" element={<ExpenseEditPage />} />
          {/* 后续可加其它 Route */}
          <Route path="*" element={<Navigate to="/" />} />
        </Routes>
      </Layout>
    </LocalizationProvider>
  );
}

export default App;


// import React from "react";
// import Dashboard from "./pages/dashboard/Dashboard";
// // 或 Layout+Routes 看你想要一级路由还是直接用 Dashboard

// function App() {
//   return (
//     <Dashboard />
//   );
// }

// export default App;