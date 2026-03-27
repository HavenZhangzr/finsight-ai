import Layout from "./components/Layout";
import { Routes, Route, Navigate } from "react-router-dom";
import BillListPage from "./pages/bill/BillListPage";
import ExpenseEditPage from "./pages/expenses/ExpenseEditPage";
import InsightDashboardPage from "./pages/insight/InsightDashboardPage";
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
          <Route path="/insights" element={<InsightDashboardPage />} />
          <Route path="*" element={<Navigate to="/" />} />
        </Routes>
      </Layout>
    </LocalizationProvider>
  );
}

export default App;
