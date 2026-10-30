SELECT id, title, release_date, rating_avg
FROM games
WHERE rating_avg >= 8
ORDER BY rating_avg DESC, title
LIMIT 10;

SELECT id, title, rating_avg
FROM games
WHERE title ILIKE '%war%'
ORDER BY rating_avg DESC, title;

SELECT u.username, r.code AS role, u.created_at
FROM users u
JOIN roles r ON r.id = u.role_id
WHERE r.code = 'critic'
ORDER BY u.username;

SELECT r.code AS role, COUNT(*) AS users_count
FROM users u
JOIN roles r ON r.id = u.role_id
GROUP BY r.code
ORDER BY users_count DESC;

SELECT g.title, v.title AS review_title, u.username AS critic, rt.score
FROM reviews v
JOIN games g ON g.id = v.game_id
JOIN users u ON u.id = v.critic_id
JOIN ratings rt ON rt.review_id = v.id
JOIN review_statuses s ON s.id = v.status_id
WHERE s.code = 'published'
ORDER BY rt.score DESC, g.title
LIMIT 20;

SELECT g.title, COUNT(v.id) AS reviews_count, ROUND(AVG(rt.score), 2) AS avg_score
FROM games g
JOIN reviews v ON v.game_id = g.id
JOIN ratings rt ON rt.review_id = v.id
GROUP BY g.id, g.title
HAVING COUNT(v.id) >= 2
ORDER BY avg_score DESC, reviews_count DESC;

SELECT g.title, g.rating_avg
FROM games g
WHERE NOT EXISTS (
 SELECT 1
 FROM reviews v
 WHERE v.game_id = g.id AND v.status_id = 2
)
ORDER BY g.title;

SELECT u.username, COUNT(v.id) AS reviews_count, ROUND(AVG(rt.score), 2) AS avg_given_score
FROM users u
JOIN reviews v ON v.critic_id = u.id
JOIN ratings rt ON rt.review_id = v.id
GROUP BY u.id, u.username
ORDER BY reviews_count DESC, avg_given_score DESC;

SELECT s.name AS status, COUNT(*) AS reviews_count
FROM reviews v
JOIN review_statuses s ON s.id = v.status_id
GROUP BY s.name
ORDER BY reviews_count DESC;

SELECT v.id, v.title, COUNT(rv.user_id) AS views_count
FROM reviews v
LEFT JOIN review_views rv ON rv.review_id = v.id
GROUP BY v.id, v.title
ORDER BY views_count DESC, v.id
LIMIT 10;

SELECT g.title, g.rating_avg
FROM games g
WHERE g.rating_avg > (SELECT AVG(rating_avg) FROM games WHERE rating_avg > 0)
ORDER BY g.rating_avg DESC;

SELECT u.username
FROM users u
WHERE u.role_id = 1
 AND NOT EXISTS (SELECT 1 FROM review_views rv WHERE rv.user_id = u.id)
ORDER BY u.username;

SELECT g.title, v.id AS review_id, rt.score,
 RANK() OVER (PARTITION BY g.id ORDER BY rt.score DESC) AS rank_in_game
FROM reviews v
JOIN games g ON g.id = v.game_id
JOIN ratings rt ON rt.review_id = v.id
ORDER BY g.title, rank_in_game
LIMIT 30;

WITH critic_stats AS (
 SELECT v.critic_id, COUNT(*) AS cnt, AVG(rt.score) AS avg_score
 FROM reviews v
 JOIN ratings rt ON rt.review_id = v.id
 GROUP BY v.critic_id
)
SELECT u.username, cs.cnt, ROUND(cs.avg_score, 2) AS avg_score
FROM critic_stats cs
JOIN users u ON u.id = cs.critic_id
WHERE cs.avg_score > (SELECT AVG(score) FROM ratings)
ORDER BY cs.avg_score DESC;

SELECT CASE
 WHEN rating_avg >= 9 THEN '9-10'
 WHEN rating_avg >= 7 THEN '7-8.99'
 WHEN rating_avg >= 5 THEN '5-6.99'
 WHEN rating_avg > 0 THEN '0-4.99'
 ELSE 'без оценок'
 END AS rating_range,
 COUNT(*) AS games_count
FROM games
GROUP BY rating_range
ORDER BY rating_range DESC;

SELECT score, COUNT(*) AS cnt
FROM ratings
GROUP BY score
ORDER BY score;

SELECT date_trunc('month', created_at)::date AS month, COUNT(*) AS reviews_count
FROM reviews
GROUP BY month
ORDER BY month;

SELECT gn.name AS genre, COUNT(gg.game_id) AS games_count
FROM genres gn
LEFT JOIN game_genres gg ON gg.genre_id = gn.id
GROUP BY gn.id, gn.name
ORDER BY games_count DESC, gn.name;