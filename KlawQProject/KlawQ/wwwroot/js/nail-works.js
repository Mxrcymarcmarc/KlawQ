document.addEventListener('DOMContentLoaded', function () {

    const favoriteButtons = document.querySelectorAll('.favorite-btn');

    favoriteButtons.forEach(button => {
        button.addEventListener('click', function (e) {

            e.preventDefault();

            this.classList.toggle('is-active');

            const itemId = this.getAttribute('data-id');
            const isNowFavorited = this.classList.contains('is-active');

            if (isNowFavorited) {
                console.log(`Item ${itemId} added to favorites!`);
            } else {
                console.log(`Item ${itemId} removed from favorites!`);
            }
        });
    });

});