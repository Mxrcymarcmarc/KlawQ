document.addEventListener('DOMContentLoaded', function(){
    const addBtn = document.getElementById('addCartBtn');
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenEl ? tokenEl.value : null;
    if(addBtn){
        addBtn.addEventListener('click', async function(e){
            e.preventDefault();
            const id = this.getAttribute('data-id');
            if(!id) return;
            this.disabled = true;
            try{
                const res = await fetch(`/Cart/add/${id}`, { method: 'POST', headers: token ? { 'RequestVerificationToken': token } : {} });
                const json = await res.json();
                if(json && json.success){
                    // update cart count in header if present
                    const cc = document.getElementById('cartCount'); if(cc) cc.textContent = json.count;
                    if(window.refreshCart) window.refreshCart();
                    alert('Added to cart');
                } else {
                    alert('Failed to add to cart');
                }
            }catch(err){ console.error(err); alert('Error adding to cart'); }
            this.disabled = false;
        });
    }
});