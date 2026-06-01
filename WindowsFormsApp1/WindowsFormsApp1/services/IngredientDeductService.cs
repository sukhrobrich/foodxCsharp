using System;
using System.Data;
using System.Data.SqlClient;

namespace WindowsFormsApp1.services
{
    /// <summary>
    /// Buyurtma tasdiqlanganida (yoki to'langanida) lokal ombordan
    /// retsept bo'yicha ingredientlarni avtomatik chiqaradi.
    /// </summary>
    internal static class IngredientDeductService
    {
        /// <param name="localConn">Ochiq lokal SQL ulanish</param>
        /// <param name="orderId">Lokal buyurtma ID</param>
        public static void Deduct(SqlConnection localConn, int orderId)
        {
            try
            {
                // Buyurtma tarkibini olish
                var orderItems = new DataTable();
                using (var da = new SqlDataAdapter(
                    new SqlCommand(
                        "SELECT food_id, quantity FROM order_food WHERE order_id=@id",
                        localConn)))
                {
                    da.SelectCommand.Parameters.AddWithValue("@id", orderId);
                    da.Fill(orderItems);
                }

                foreach (DataRow item in orderItems.Rows)
                {
                    int     foodId     = Convert.ToInt32(item["food_id"]);
                    decimal orderedQty = Convert.ToDecimal(item["quantity"]);

                    // Retsept ingredientlarini olish
                    var recipe = new DataTable();
                    using (var da = new SqlDataAdapter(
                        new SqlCommand(
                            "SELECT ingredient_id, quantity_per_portion FROM recipe_ingredient WHERE food_id=@fid",
                            localConn)))
                    {
                        da.SelectCommand.Parameters.AddWithValue("@fid", foodId);
                        da.Fill(recipe);
                    }

                    foreach (DataRow ri in recipe.Rows)
                    {
                        int     ingId    = Convert.ToInt32(ri["ingredient_id"]);
                        decimal toDeduct = Convert.ToDecimal(ri["quantity_per_portion"]) * orderedQty;

                        using (var cmd = new SqlCommand(
                            @"UPDATE ingredient
                              SET quantity = CASE WHEN ISNULL(quantity,0) >= @d
                                                 THEN quantity - @d ELSE 0 END
                              WHERE id = @iid",
                            localConn))
                        {
                            cmd.Parameters.AddWithValue("@d",   toDeduct);
                            cmd.Parameters.AddWithValue("@iid", ingId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Cheklanmagan emas bo'lsa taom sonini ham kamaytir
                    using (var cmd = new SqlCommand(
                        @"UPDATE food
                          SET count = CASE WHEN is_unlimited=0 AND ISNULL(count,0) >= @q
                                          THEN count - @q ELSE count END
                          WHERE id = @fid",
                        localConn))
                    {
                        cmd.Parameters.AddWithValue("@q",   orderedQty);
                        cmd.Parameters.AddWithValue("@fid", foodId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("IngredientDeduct xatosi: " + ex.Message);
                // Ingredient chiqarish muvaffaqiyatsiz bo'lsa ham buyurtma to'xtatilmaydi
            }
        }
    }
}
