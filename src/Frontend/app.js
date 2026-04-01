const API_BASE = 'http://localhost:5002/api';

document.addEventListener('DOMContentLoaded', () => {
    loadProducts();
    loadOrders();

    document.getElementById('product-form').addEventListener('submit', createProduct);
    document.getElementById('order-form').addEventListener('submit', createOrder);
});

async function loadProducts() {
    const list = document.getElementById('product-list');
    const select = document.getElementById('order-prod-id');
    try {
        const response = await fetch(`${API_BASE}/products`);
        if (!response.ok) throw new Error('Network response was not ok');
        const products = await response.json();
        
        list.innerHTML = '';
        // Reset select dropdown (keep first option)
        select.innerHTML = '<option value="" disabled selected>Select a Product</option>';

        if (products.length === 0) {
            list.innerHTML = '<div class="loading">No products found.</div>';
            return;
        }

        products.forEach(p => {
            // Add to list
            list.innerHTML += `
                <div class="item-card">
                    <div class="item-title">
                        <span>${p.name}</span>
                        <span class="item-price">$${p.price.toFixed(2)}</span>
                    </div>
                    <div class="item-desc">${p.description} (ID: ${p.id})</div>
                </div>
            `;
            // Add to dropdown
            select.innerHTML += `<option value="${p.id}">${p.name} - $${p.price}</option>`;
        });
    } catch (error) {
        list.innerHTML = `<div class="loading" style="color:var(--error)">Error loading products. Is the backend running?</div>`;
        console.error('Error fetching products:', error);
    }
}

async function loadOrders() {
    const list = document.getElementById('order-list');
    try {
        const response = await fetch(`${API_BASE}/orders`);
        if (!response.ok) throw new Error('Network response was not ok');
        const orders = await response.json();
        
        list.innerHTML = '';
        if (orders.length === 0) {
            list.innerHTML = '<div class="loading">No orders yet.</div>';
            return;
        }

        orders.forEach(o => {
            list.innerHTML += `
                <div class="item-card order-card">
                    <div class="item-title">
                        <span>Order #${o.id}</span>
                        <span class="item-price">$${o.totalPrice.toFixed(2)}</span>
                    </div>
                    <div class="item-desc">Product ID: ${o.productId} | Qty: ${o.quantity} <br> <small>${new Date(o.orderDate).toLocaleString()}</small></div>
                </div>
            `;
        });
    } catch (error) {
        list.innerHTML = `<div class="loading" style="color:var(--error)">Error loading orders.</div>`;
        console.error('Error fetching orders:', error);
    }
}

async function createProduct(e) {
    e.preventDefault();
    const msg = document.getElementById('prod-msg');
    msg.textContent = 'Saving...';
    msg.className = 'msg';

    const product = {
        name: document.getElementById('prod-name').value,
        price: parseFloat(document.getElementById('prod-price').value),
        description: document.getElementById('prod-desc').value
    };

    try {
        const response = await fetch(`${API_BASE}/products`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(product)
        });

        if (response.ok) {
            msg.textContent = 'Product created!';
            msg.className = 'msg success';
            document.getElementById('product-form').reset();
            loadProducts(); // Refresh list
        } else {
            throw new Error('Failed to create');
        }
    } catch (error) {
        msg.textContent = 'Error creating product.';
        msg.className = 'msg error';
    }
}

async function createOrder(e) {
    e.preventDefault();
    const msg = document.getElementById('order-msg');
    msg.textContent = 'Placing order...';
    msg.className = 'msg';

    const order = {
        productId: parseInt(document.getElementById('order-prod-id').value),
        quantity: parseInt(document.getElementById('order-qty').value)
    };

    try {
        const response = await fetch(`${API_BASE}/orders`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(order)
        });

        if (response.ok) {
            msg.textContent = 'Order placed!';
            msg.className = 'msg success';
            document.getElementById('order-form').reset();
            loadOrders(); // Refresh list
        } else {
            throw new Error('Failed to place order');
        }
    } catch (error) {
        msg.textContent = 'Error placing order. Product might not exist.';
        msg.className = 'msg error';
    }
}
